using DailyMachineSpirit.Data.Repositories;
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

    // What the page says after each action, by the key in its address.
    private static readonly Dictionary<string, string> Outcomes = new()
    {
        ["anoint"] = "Anointed: it goes out next.",
        ["exalt"] = "Exalted.",
        ["humble"] = "Humbled.",
        ["burn"] = "Consigned to the flames.",
        ["burn-last"] = "Consigned to the flames. No rite waits now, so new ones are being summoned: look again in a few minutes.",
        ["restore"] = "Restored from the ashes.",
        ["summon"] = "New rites are being summoned: look again in a few minutes.",
        ["augury"] = "The Augury orders every draft again.",
        ["changed"] = "Another Scribe changed the calendar meanwhile. Look again before deciding.",
        ["note-too-long"] = "That note is too long, so nothing was changed. Shorten it and try again.",
    };

    private readonly Scribes scribes;
    private readonly IOptions<ScriptoriumOptions> options;
    private readonly ILogger<ScriptoriumFunction> logger;

    public ScriptoriumFunction(Scribes scribes, IOptions<ScriptoriumOptions> options, ILogger<ScriptoriumFunction> logger)
    {
        this.scribes = scribes;
        this.options = options;
        this.logger = logger;
    }

    [Function("Scriptorium")]
    public async Task<IActionResult> Page(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium")] HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return new NotFoundResult();

        var status = Optional(request.Query["done"].ToString()).Bind(key => Outcomes.TryGetValue(key, out var text) ? Some(text) : None);
        return (await scribes.View(cancellationToken)).Match(
            Right: view => Html(request, ScriptoriumPage.Render(view, status), StatusCodes.Status200OK),
            Left: error => Failed(request, error));
    }

    [Function("ScriptoriumDecision")]
    public async Task<ScriptoriumResponse> Decide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/drafts/{draftId:guid}/{action:alpha}")] HttpRequest request,
        Guid draftId,
        string action,
        CancellationToken cancellationToken)
        => await Refusal(request).Match(
            Some: refused => Task.FromResult(new ScriptoriumResponse { Result = refused }),
            None: () => Act(request, draftId, action, cancellationToken));

    [Function("ScriptoriumSummon")]
    public ScriptoriumResponse Summon(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/summon")] HttpRequest request)
        => Refusal(request).Match(
            Some: refused => new ScriptoriumResponse { Result = refused },
            None: () => new ScriptoriumResponse { Result = BackToPage("summon"), RefillReason = Summoned });

    [Function("ScriptoriumAugury")]
    public async Task<IActionResult> LetTheAuguryDecide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/augury")] HttpRequest request, CancellationToken cancellationToken)
        => await Refusal(request).Match(
            Some: refused => Task.FromResult(refused),
            None: async () => Answer(request, "augury", await scribes.LetTheAuguryDecide(cancellationToken)).Result);

    private async Task<ScriptoriumResponse> Act(HttpRequest request, Guid draftId, string action, CancellationToken cancellationToken)
    {
        var note = (await request.ReadFormAsync(cancellationToken))["note"].ToString();
        return action switch
        {
            "anoint" => Answer(request, action, await scribes.Anoint(draftId, note, cancellationToken)),
            "exalt" => Answer(request, action, await scribes.Exalt(draftId, note, cancellationToken)),
            "humble" => Answer(request, action, await scribes.Humble(draftId, note, cancellationToken)),
            "restore" => Answer(request, action, await scribes.Restore(draftId, note, cancellationToken)),
            "burn" => (await scribes.Burn(draftId, note, cancellationToken)).Match(
                // Nothing waits any more: refill now rather than at the next daily run.
                Right: waiting => waiting == 0
                    ? new ScriptoriumResponse { Result = BackToPage("burn-last"), RefillReason = AllBurned }
                    : new ScriptoriumResponse { Result = BackToPage("burn") },
                Left: error => Failure(request, error)),
            _ => new ScriptoriumResponse { Result = new NotFoundResult() },
        };
    }

    // Turned off, the Scriptorium doesn't exist. A form posted from another site (a forged request riding a Scribe's
    // sign-in) is forbidden: browsers say where a request comes from in Sec-Fetch-Site, and a request without it isn't
    // from a browser, so it carries no sign-in to abuse.
    private Option<IActionResult> Refusal(HttpRequest request)
    {
        if (!options.Value.Enabled)
            return new NotFoundResult();
        return request.Headers["Sec-Fetch-Site"].ToString() is "" or "same-origin"
            ? None
            : new StatusCodeResult(StatusCodes.Status403Forbidden);
    }

    private ScriptoriumResponse Answer(HttpRequest request, string action, Either<Error, Unit> result)
        => result.Match(
            Right: _ => new ScriptoriumResponse { Result = BackToPage(action) },
            Left: error => Failure(request, error));

    private ScriptoriumResponse Failure(HttpRequest request, Error error)
    {
        if (error == ScriptoriumRepository.ChangedMeanwhile)
            return new ScriptoriumResponse { Result = BackToPage("changed") };
        if (error == Scribes.NoteTooLong)
            return new ScriptoriumResponse { Result = BackToPage("note-too-long") };
        return new ScriptoriumResponse { Result = Failed(request, error) };
    }

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The Scriptorium failed: {Reason}", error.Message);
        return Html(request, ScriptoriumPage.RenderError(), StatusCodes.Status500InternalServerError);
    }

    // A 302, which browsers follow with a GET.
    private static IActionResult BackToPage(string outcome)
        => new RedirectResult($"/scriptorium?done={outcome}") { PreserveMethod = false, Permanent = false };

    private static IActionResult Html(HttpRequest request, string html, int status)
    {
        // The Scribes' page is never cached (by Cloudflare or the browser) and never indexed.
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        request.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}
