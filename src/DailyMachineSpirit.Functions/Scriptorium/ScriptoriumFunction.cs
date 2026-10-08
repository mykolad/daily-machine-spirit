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
    public const string Uncounted = "the backlog couldn't be counted after a burn";

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
        var form = await ReadForm(request, cancellationToken);
        if (form.IsNone)
            return new ScriptoriumResponse { Result = new BadRequestResult() };
        var note = form.Map(fields => fields["note"].ToString()).IfNone(string.Empty);
        return action switch
        {
            "anoint" => Answer(request, action, await scribes.Anoint(draftId, note, cancellationToken)),
            "exalt" => Answer(request, action, await scribes.Exalt(draftId, note, cancellationToken)),
            "humble" => Answer(request, action, await scribes.Humble(draftId, note, cancellationToken)),
            "restore" => Answer(request, action, await scribes.Restore(draftId, note, cancellationToken)),
            "burn" => (await scribes.Burn(draftId, note, cancellationToken)).Match(
                // Nothing waits any more, or it can't be told: refill now rather than at the next daily run. A refill
                // only writes what's missing, so an unneeded one costs a read.
                Right: waiting => waiting.Match(
                    Some: count => count == 0
                        ? new ScriptoriumResponse { Result = BackToPage("burn-last"), RefillReason = AllBurned }
                        : new ScriptoriumResponse { Result = BackToPage("burn") },
                    None: () => new ScriptoriumResponse { Result = BackToPage("burn"), RefillReason = Uncounted }),
                Left: error => Failure(request, error)),
            _ => new ScriptoriumResponse { Result = new NotFoundResult() },
        };
    }

    // The page's forms are always url-encoded; anything else is the client's mistake (400), not the Scriptorium's.
    private static async Task<Option<IFormCollection>> ReadForm(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
            return None;
        try
        {
            return Some(await request.ReadFormAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is (InvalidDataException or IOException) && !cancellationToken.IsCancellationRequested)
        {
            return None;
        }
    }

    // Turned off, the Scriptorium doesn't exist. A form posted from another site (a forged request riding a Scribe's
    // sign-in or address) is forbidden. Browsers say where a request comes from in Sec-Fetch-Site; ones too old for that
    // still send Origin with a form post, which must then be this site. A request with neither is refused too.
    private Option<IActionResult> Refusal(HttpRequest request)
    {
        if (!options.Value.Enabled)
            return new NotFoundResult();
        return IsFromThisSite(request) ? None : new StatusCodeResult(StatusCodes.Status403Forbidden);
    }

    private static bool IsFromThisSite(HttpRequest request)
    {
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0)
            return fetchSite == "same-origin";
        return Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
            && string.Equals(origin.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
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
