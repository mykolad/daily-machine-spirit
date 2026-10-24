using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Precepts;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Functions.Scriptorium.ScriptoriumHttp;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// <c>/scriptorium/precepts</c> (#12): the Precepts, and the Scribes' changes to them. Each action redirects back to the
/// page with what happened, as the Scriptorium's do.
/// </summary>
public sealed class PreceptsFunction
{
    private readonly PreceptKeeping keeping;
    private readonly PreceptDistiller distiller;
    private readonly AgreementTrend trend;
    private readonly TabCounting tabs;
    private readonly NamesShown names;
    private readonly TimeProvider time;
    private readonly ScriptoriumGate gate;
    private readonly ILogger<PreceptsFunction> logger;

    public PreceptsFunction(
        PreceptKeeping keeping,
        PreceptDistiller distiller,
        AgreementTrend trend,
        TabCounting tabs,
        NamesShown names,
        IScribeSignIn signIn,
        TimeProvider time,
        ILogger<PreceptsFunction> logger)
    {
        this.keeping = keeping;
        this.distiller = distiller;
        this.trend = trend;
        this.tabs = tabs;
        this.names = names;
        this.time = time;
        gate = new ScriptoriumGate(signIn);
        this.logger = logger;
    }

    [Function("ScriptoriumPrecepts")]
    public async Task<IActionResult> Page(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/precepts")] HttpRequest request, CancellationToken cancellationToken)
    {
        var looking = await gate.LookingScribe(request, cancellationToken);
        if (looking.Case is IActionResult refused)
        {
            return refused;
        }
        var scribe = (ScribeId)looking.Case;

        var outcome = Optional(request.Query["done"].ToString()).Filter(done => done.Length > 0);
        var acted = Id(request.Query["precept"].ToString());
        var shown = await Shown(Id(request.Query["edit"].ToString()), scribe, cancellationToken);
        var counts = await tabs.Count(cancellationToken);
        return (await PreceptsAndTrend(cancellationToken)).Match(
            Right: found => Html(request, ScriptoriumPage.RenderPrecepts(found.Stored, counts, found.Months, outcome, acted, None, shown), StatusCodes.Status200OK),
            Left: error => Failed(request, error));
    }

    // A Scribe waits for the model's answer: a minute at most, far less than a request may take.
    [Function("ScriptoriumPreceptsDistil")]
    public async Task<IActionResult> Distil(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/precepts/distil")] HttpRequest request,
        CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(refused),
            Right: async scribe => (await distiller.Distil(scribe, cancellationToken)).Match(
                Right: _ =>
                {
                    logger.CountScribeAction("distil");
                    return Back("distil", None);
                },
                Left: error =>
                {
                    if (error == PreceptDistiller.NothingToLearn)
                    {
                        return Back("distil-nothing", None);
                    }
                    if (error == PreceptDistiller.AllPinned)
                    {
                        return Back("distil-pinned", None);
                    }
                    if (error == PreceptDistiller.NothingWithinTheRules)
                    {
                        return Back("distil-refused", None);
                    }
                    if (error == PreceptDistiller.Unchecked)
                    {
                        return Back("distil-unchecked", None);
                    }
                    if (error == ScriptoriumRepository.ChangedMeanwhile)
                    {
                        return Back("changed", None);
                    }
                    logger.LogError(error.ToException(), "The Precepts couldn't be distilled: {Reason}", error.Message);
                    return Back("distil-failed", None);
                }));

    [Function("ScriptoriumPreceptsAdd")]
    public async Task<IActionResult> Add(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/precepts/add")] HttpRequest request,
        CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(refused),
            Right: async scribe => await (await ReadForm(request, cancellationToken)).Match(
                Some: async fields =>
                {
                    // Back to the new precept, marked, as after any change to one.
                    var added = await keeping.AddPinned(fields["text"].ToString(), scribe, cancellationToken);
                    return await Saved(request, "add", added.ToOption(), fields["text"].ToString(), added.Map(_ => unit), scribe, cancellationToken);
                },
                None: () => Task.FromResult<IActionResult>(new BadRequestResult())));

    [Function("ScriptoriumPrecept")]
    public async Task<IActionResult> Change(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/precepts/{preceptId:guid}/{action:alpha}")] HttpRequest request,
        Guid preceptId,
        string action,
        CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(refused),
            Right: async scribe => await (await ReadForm(request, cancellationToken)).Match(
                Some: fields => Act(request, preceptId, action, fields, scribe, cancellationToken),
                None: () => Task.FromResult<IActionResult>(new BadRequestResult())));

    // Each change carries the version of the Precepts the Scribe's page showed (etag).
    private async Task<IActionResult> Act(
        HttpRequest request, Guid preceptId, string action, IFormCollection fields, ScribeId scribe, CancellationToken cancellationToken)
    {
        var etag = fields["etag"].ToString();
        return action switch
        {
            "edit" => await Saved(request, action, preceptId, fields["text"].ToString(),
                await keeping.EditAndPin(preceptId, etag, fields["text"].ToString(), scribe, cancellationToken), scribe, cancellationToken),
            "pin" => Answer(request, action, preceptId, await keeping.Pin(preceptId, etag, scribe, cancellationToken)),
            "unpin" => Answer(request, action, preceptId, await keeping.Unpin(preceptId, etag, scribe, cancellationToken)),
            // Gone from the page, so the page stays at the top, where the Undo names it.
            "delete" => (await keeping.Delete(preceptId, etag, scribe, cancellationToken)).Match(
                Right: _ =>
                {
                    logger.CountScribeAction("precept-delete");
                    return Redirect($"/scriptorium/precepts?done=delete&precept={preceptId:D}");
                },
                Left: error => Answer(request, action, None, Left<Error, Unit>(error))),
            "restore" => Answer(request, action, preceptId, await keeping.RestoreDeleted(preceptId, etag, scribe, cancellationToken)),
            _ => new NotFoundResult(),
        };
    }

    // Refused for breaking the writer's rules, or because they couldn't be checked: the page again, with the Scribe's
    // words in their field and why (422), rather than a redirect, which couldn't carry them.
    private async Task<IActionResult> Saved(
        HttpRequest request, string action, Option<Guid> preceptId, string text, Either<Error, Unit> result, ScribeId scribe,
        CancellationToken cancellationToken)
    {
        var nextStep = result.Case switch
        {
            BreaksARule => "Reword it, or leave it as it was.",
            Error error when error == PreceptKeeping.Unchecked => "Try again in a moment.",
            _ => string.Empty,
        };
        if (nextStep.Length == 0 || result.Case is not Error refusal)
        {
            return Answer(request, action, preceptId, result);
        }
        var refused = new RefusedPrecept(preceptId, text, refusal.Message, nextStep);
        var counts = await tabs.Count(cancellationToken);
        var shown = await Shown(None, scribe, cancellationToken);
        return (await PreceptsAndTrend(cancellationToken)).Match(
            Right: found => Html(request, ScriptoriumPage.RenderPrecepts(found.Stored, counts, found.Months, None, preceptId, Some(refused), shown),
                StatusCodes.Status422UnprocessableEntity),
            Left: error => Failed(request, error));
    }

    private async Task<PreceptsShown> Shown(Option<Guid> editing, ScribeId scribe, CancellationToken cancellationToken)
        => new(editing, time.GetUtcNow().UtcDateTime, await names.Of(scribe, cancellationToken), await names.All(cancellationToken));

    private static Option<Guid> Id(string text) => Guid.TryParse(text, out var id) ? Some(id) : None;

    private Task<Either<Error, (StoredPrecepts Stored, List<AgreementMonth> Months)>> PreceptsAndTrend(CancellationToken cancellationToken)
        => keeping.View(cancellationToken)
            .BindAsync(stored => trend.Months(cancellationToken).MapAsync(months => Task.FromResult((stored, months))));

    private IActionResult Answer(HttpRequest request, string action, Option<Guid> preceptId, Either<Error, Unit> result)
        => result.Match(
            Right: _ =>
            {
                logger.CountScribeAction($"precept-{action}");
                return Back(action, preceptId);
            },
            Left: error =>
            {
                if (error == ScriptoriumRepository.ChangedMeanwhile)
                {
                    return Back("changed", None);
                }
                if (error == PreceptKeeping.TextNeeded)
                {
                    return Back("text-needed", preceptId);
                }
                if (error == PreceptKeeping.TooLong)
                {
                    return Back("too-long", preceptId);
                }
                if (error == PreceptKeeping.Full)
                {
                    return Back("full", None);
                }
                return Failed(request, error);
            });

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The Precepts failed: {Reason}", error.Message);
        return Html(request, ScriptoriumPage.RenderError(), StatusCodes.Status500InternalServerError);
    }

    // Back to the precept acted on, where it is on the page, marked.
    private static IActionResult Back(string outcome, Option<Guid> preceptId)
        => preceptId.Match(
            Some: id => Redirect($"/scriptorium/precepts?done={outcome}&precept={id:D}#precept-{id:D}"),
            None: () => Redirect($"/scriptorium/precepts?done={outcome}"));
}
