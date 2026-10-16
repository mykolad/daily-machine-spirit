using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>A Scriptorium answer: the page to go to, and a refill request when one is needed.</summary>
public sealed class ScriptoriumResponse
{
    [HttpResult]
    public IActionResult Result { get; init; } = new NotFoundResult();

    // Null when nothing needs refilling: the binding sends no message then.
    [QueueOutput(RefillBacklogFunction.QueueName)]
    public string? RefillReason { get; init; }
}

/// <summary>
/// <c>/scriptorium</c>: the Scribes' page, and the actions it posts. Each action redirects back to the page with what
/// happened (post, redirect, get), so reloading never repeats it.
/// </summary>
public sealed class ScriptoriumFunction
{
    public const string Summoned = "summoned by a Scribe";
    public const string AllBurned = "every draft was burned";
    public const string Uncounted = "the backlog couldn't be counted after a burn";

    /// <summary>How many entries the Record shows: the newest.</summary>
    public const int RecordShown = 100;

    private readonly Scribes scribes;
    private readonly Amendments amendments;
    private readonly IAuditRepository audit;
    private readonly TimeProvider time;
    private readonly IScribeSignIn signIn;
    private readonly IOptions<ScriptoriumOptions> options;
    private readonly ILogger<ScriptoriumFunction> logger;

    public ScriptoriumFunction(
        Scribes scribes,
        Amendments amendments,
        IAuditRepository audit,
        IScribeSignIn signIn,
        IOptions<ScriptoriumOptions> options,
        TimeProvider time,
        ILogger<ScriptoriumFunction> logger)
    {
        this.scribes = scribes;
        this.amendments = amendments;
        this.audit = audit;
        this.time = time;
        this.signIn = signIn;
        this.options = options;
        this.logger = logger;
    }

    [Function("Scriptorium")]
    public async Task<IActionResult> Page(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium")] HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return new NotFoundResult();
        }
        if (!await signIn.IsScribe(request, cancellationToken))
        {
            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        }

        var outcome = Optional(request.Query["done"].ToString()).Filter(done => done.Length > 0);
        var rite = Optional(request.Query["rite"].ToString()).Bind(text => Guid.TryParse(text, out var id) ? Some(id) : None);
        return (await scribes.View(cancellationToken)).Match(
            Right: view => Html(request, ScriptoriumPage.Render(view, outcome, rite), StatusCodes.Status200OK),
            Left: error => Failed(request, error));
    }

    /// <summary>The page for amending a waiting draft's words (#8); its form posts the amend and rewrite actions.</summary>
    [Function("ScriptoriumAmend")]
    public async Task<IActionResult> AmendPage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/drafts/{draftId:guid}/amend")] HttpRequest request,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return new NotFoundResult();
        }
        if (!await signIn.IsScribe(request, cancellationToken))
        {
            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        }

        return (await amendments.Get(draftId, cancellationToken)).Match(
            Right: found => found.Match(
                Some: amendable => Html(request, ScriptoriumPage.RenderAmend(amendable, Words(amendable.Draft), string.Empty, None), StatusCodes.Status200OK),
                // Burned or published since the calendar was shown: the page says to look again.
                None: () => Back("changed", draftId)),
            Left: error => Failed(request, error));
    }

    /// <summary>The Record (#9): every change and who made it, newest first; <c>?draft=</c> for one draft's.</summary>
    [Function("ScriptoriumRecord")]
    public async Task<IActionResult> Record(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/record")] HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return new NotFoundResult();
        }
        if (!await signIn.IsScribe(request, cancellationToken))
        {
            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        }

        var draft = Optional(request.Query["draft"].ToString()).Bind(text => Guid.TryParse(text, out var id) ? Some(id) : None);
        return (await audit.GetNewest(RecordShown, draft, cancellationToken)).Match(
            Right: entries => Html(request, ScriptoriumPage.RenderRecord(entries, options.Value.Scribes, draft), StatusCodes.Status200OK),
            Left: error => Failed(request, error));
    }

    [Function("ScriptoriumDecision")]
    public async Task<ScriptoriumResponse> Decide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/drafts/{draftId:guid}/{action:alpha}")] HttpRequest request,
        Guid draftId,
        string action,
        CancellationToken cancellationToken)
        => await (await ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(new ScriptoriumResponse { Result = refused }),
            Right: scribe => Act(request, draftId, action, scribe, cancellationToken));

    [Function("ScriptoriumSummon")]
    public async Task<ScriptoriumResponse> Summon(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/summon")] HttpRequest request, CancellationToken cancellationToken)
        => await (await ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(new ScriptoriumResponse { Result = refused }),
            Right: async scribe =>
            {
                // The summon changes nothing stored itself (the refill writes the drafts, each recorded as the Machine
                // Spirit's), so its entry is saved alone. Failing, it's logged, and the summon still goes ahead.
                (await audit.Add(AuditEntries.Of(time, scribe, AuditAction.Summon, None, None), cancellationToken))
                    .IfLeft(error => logger.LogError(error.ToException(), "A summon wasn't recorded: {Reason}", error.Message));
                return new ScriptoriumResponse { Result = BackToPage("summon"), RefillReason = Summoned };
            });

    [Function("ScriptoriumAugury")]
    public async Task<IActionResult> LetTheAuguryDecide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/augury")] HttpRequest request, CancellationToken cancellationToken)
        => await (await ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(refused),
            Right: async scribe => Answer(request, "augury", None, await scribes.LetTheAuguryDecide(scribe, cancellationToken)).Result);

    private async Task<ScriptoriumResponse> Act(HttpRequest request, Guid draftId, string action, string scribe, CancellationToken cancellationToken)
        => await (await ReadForm(request, cancellationToken)).Match(
            Some: fields => Act(request, draftId, action, fields, scribe, cancellationToken),
            None: () => Task.FromResult(new ScriptoriumResponse { Result = new BadRequestResult() }));

    private async Task<ScriptoriumResponse> Act(
        HttpRequest request, Guid draftId, string action, IFormCollection fields, string scribe, CancellationToken cancellationToken)
    {
        var note = fields["note"].ToString();
        return action switch
        {
            "amend" => await Amend(request, draftId, fields, scribe, cancellationToken),
            "rewrite" => await Rewrite(request, draftId, fields, cancellationToken),
            "anoint" => Answer(request, action, draftId, await scribes.Anoint(draftId, note, scribe, cancellationToken)),
            "exalt" => Answer(request, action, draftId, await scribes.Exalt(draftId, note, scribe, cancellationToken)),
            "humble" => Answer(request, action, draftId, await scribes.Humble(draftId, note, scribe, cancellationToken)),
            "restore" => Answer(request, action, draftId, await scribes.Restore(draftId, note, scribe, cancellationToken)),
            "burn" => (await scribes.Burn(draftId, note, scribe, cancellationToken)).Match(
                // Nothing waits any more, or it can't be told: refill now rather than at the next daily run. A refill
                // only writes what's missing, so an unneeded one costs a read.
                Right: waiting => waiting.Match(
                    Some: count => count == 0
                        ? new ScriptoriumResponse { Result = AfterBurning("burn-last", draftId), RefillReason = AllBurned }
                        : new ScriptoriumResponse { Result = AfterBurning("burn", draftId) },
                    None: () => new ScriptoriumResponse { Result = AfterBurning("burn", draftId), RefillReason = Uncounted }),
                Left: error => Failure(request, error, draftId)),
            _ => new ScriptoriumResponse { Result = new NotFoundResult() },
        };
    }

    // Saved and judged again, back to the calendar; refused, the same page again with the Scribe's words and why.
    private async Task<ScriptoriumResponse> Amend(
        HttpRequest request, Guid draftId, IFormCollection fields, string scribe, CancellationToken cancellationToken)
    {
        var words = Words(fields);
        var note = fields["note"].ToString();
        var etag = fields["etag"].ToString();
        return await (await amendments.Save(draftId, etag, words, note, scribe, cancellationToken)).Match(
            Right: judged => Task.FromResult(new ScriptoriumResponse { Result = Back(judged ? "amend" : "amend-unjudged", draftId) }),
            Left: error => IsTheScribes(error)
                ? AmendAgain(request, draftId, etag, words, note, error.Message, StatusCodes.Status422UnprocessableEntity, cancellationToken)
                : Task.FromResult(Failure(request, error, draftId)));
    }

    // The rewrite goes into the form, unsaved: the Scribe reads it before saving it.
    private async Task<ScriptoriumResponse> Rewrite(HttpRequest request, Guid draftId, IFormCollection fields, CancellationToken cancellationToken)
    {
        var words = Words(fields);
        var note = fields["note"].ToString();
        var etag = fields["etag"].ToString();
        return await (await amendments.Rewrite(draftId, words, note, cancellationToken)).Match(
            Right: rewritten => AmendAgain(request, draftId, etag, rewritten, note,
                "The Machine Spirit rewrote it as your note asks. Read it over, then save it, ask again, or leave it as it was.",
                StatusCodes.Status200OK, cancellationToken),
            Left: error =>
            {
                if (error == ScriptoriumRepository.ChangedMeanwhile)
                {
                    return Task.FromResult(new ScriptoriumResponse { Result = Back("changed", draftId) });
                }
                if (IsTheScribes(error))
                {
                    return AmendAgain(request, draftId, etag, words, note, error.Message, StatusCodes.Status422UnprocessableEntity, cancellationToken);
                }

                logger.LogError(error.ToException(), "The draft {DraftId} couldn't be rewritten: {Reason}", draftId, error.Message);
                return AmendAgain(request, draftId, etag, words, note,
                    "The Machine Spirit couldn't rewrite it just now. Try again in a moment, or amend it by hand.",
                    StatusCodes.Status503ServiceUnavailable, cancellationToken);
            });
    }

    // The amending page again, keeping the version the Scribe started from, so a change made meanwhile still stops the
    // save; gone from the calendar since, back to it to look again.
    private async Task<ScriptoriumResponse> AmendAgain(
        HttpRequest request, Guid draftId, string etag, RiteContent words, string note, string message, int status, CancellationToken cancellationToken)
        => (await amendments.Get(draftId, cancellationToken)).Match(
            Right: found => found.Match(
                Some: amendable => new ScriptoriumResponse
                {
                    Result = Html(request, ScriptoriumPage.RenderAmend(amendable with { ETag = etag }, words, note, Some(message)), status),
                },
                None: () => new ScriptoriumResponse { Result = Back("changed", draftId) }),
            Left: error => Failure(request, error, draftId));

    // The Scribe's to fix on the page: words breaking a rule, a note too long, or no note to rewrite by.
    private static bool IsTheScribes(Error error) => error.Code == Amendments.Refused || error == Scribes.NoteTooLong;

    private static RiteContent Words(IFormCollection fields)
        => new(fields["title"].ToString(), fields["text"].ToString(), fields["truth"].ToString());

    private static RiteContent Words(Draft draft) => new(draft.Writing.Title, draft.Writing.Text, draft.Writing.HereticalTruth);

    // The page's forms are always url-encoded; anything else is the client's mistake (400), not the Scriptorium's.
    private static async Task<Option<IFormCollection>> ReadForm(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return None;
        }
        try
        {
            return Some(await request.ReadFormAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is (InvalidDataException or IOException) && !cancellationToken.IsCancellationRequested)
        {
            return None;
        }
    }

    // Turned off, the Scriptorium doesn't exist. Only a Scribe may act (IScribeSignIn), and a form posted from another
    // site (a forged request riding a Scribe's sign-in or address) is forbidden.
    // Which Scribe acts, for the record (#9); else why they may not.
    private async Task<Either<IActionResult, string>> ActingScribe(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return new NotFoundResult();
        }
        if (!RequestOrigin.IsFromThisSite(request))
        {
            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
        return (await signIn.Scribe(request, cancellationToken))
            .ToEither<IActionResult>(() => new StatusCodeResult(StatusCodes.Status403Forbidden));
    }

    private ScriptoriumResponse Answer(HttpRequest request, string action, Option<Guid> draftId, Either<Error, Unit> result)
        => result.Match(
            Right: _ => new ScriptoriumResponse { Result = Back(action, draftId) },
            Left: error => Failure(request, error, draftId));

    private ScriptoriumResponse Failure(HttpRequest request, Error error, Option<Guid> draftId)
    {
        if (error == ScriptoriumRepository.ChangedMeanwhile)
        {
            return new ScriptoriumResponse { Result = Back("changed", draftId) };
        }
        if (error == Scribes.NothingToMove)
        {
            return new ScriptoriumResponse { Result = Back("unmoved", draftId) };
        }
        if (error == Scribes.NoteTooLong)
        {
            return new ScriptoriumResponse { Result = Back("note-too-long", draftId) };
        }
        return new ScriptoriumResponse { Result = Failed(request, error) };
    }

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The Scriptorium failed: {Reason}", error.Message);
        return Html(request, ScriptoriumPage.RenderError(), StatusCodes.Status500InternalServerError);
    }

    private static IActionResult BackToPage(string outcome) => Redirect($"/scriptorium?done={outcome}");

    // Back to the rite the Scribe acted on, where they were on the page, and the message names it.
    private static IActionResult Back(string outcome, Option<Guid> draftId)
        => draftId.Match(
            Some: id => Redirect($"/scriptorium?done={outcome}&rite={id:D}#rite-{id:D}"),
            None: () => BackToPage(outcome));

    // A burned rite has left the calendar, so the page stays at the message, which links to the rite in the Ashes.
    private static IActionResult AfterBurning(string outcome, Guid draftId) => Redirect($"/scriptorium?done={outcome}&rite={draftId:D}");

    // A 302, which browsers follow with a GET.
    private static IActionResult Redirect(string url) => new RedirectResult(url) { PreserveMethod = false, Permanent = false };

    private static IActionResult Html(HttpRequest request, string html, int status)
    {
        // The Scribes' page is never cached (by Cloudflare or the browser) and never indexed. It's never shown inside
        // another site's frame either, where disguised buttons could trick a Scribe into burning or reordering
        // (clickjacking); X-Frame-Options covers browsers without frame-ancestors.
        var headers = request.HttpContext.Response.Headers;
        headers.CacheControl = "no-store";
        headers["X-Robots-Tag"] = "noindex";
        headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        headers.XFrameOptions = "DENY";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}
