using DailyMachineSpirit.Data.Repositories;
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

    private readonly Scribes scribes;
    private readonly IScribeSignIn signIn;
    private readonly IOptions<ScriptoriumOptions> options;
    private readonly ILogger<ScriptoriumFunction> logger;

    public ScriptoriumFunction(
        Scribes scribes, IScribeSignIn signIn, IOptions<ScriptoriumOptions> options, ILogger<ScriptoriumFunction> logger)
    {
        this.scribes = scribes;
        this.signIn = signIn;
        this.options = options;
        this.logger = logger;
    }

    [Function("Scriptorium")]
    public async Task<IActionResult> Page(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium")] HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return new NotFoundResult();
        if (!await signIn.IsScribe(request, cancellationToken))
            return new StatusCodeResult(StatusCodes.Status403Forbidden);

        var outcome = Optional(request.Query["done"].ToString()).Filter(done => done.Length > 0);
        var rite = Optional(request.Query["rite"].ToString()).Bind(text => Guid.TryParse(text, out var id) ? Some(id) : None);
        return (await scribes.View(cancellationToken)).Match(
            Right: view => Html(request, ScriptoriumPage.Render(view, outcome, rite), StatusCodes.Status200OK),
            Left: error => Failed(request, error));
    }

    [Function("ScriptoriumDecision")]
    public async Task<ScriptoriumResponse> Decide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/drafts/{draftId:guid}/{action:alpha}")] HttpRequest request,
        Guid draftId,
        string action,
        CancellationToken cancellationToken)
        => await (await Refusal(request, cancellationToken)).Match(
            Some: refused => Task.FromResult(new ScriptoriumResponse { Result = refused }),
            None: () => Act(request, draftId, action, cancellationToken));

    [Function("ScriptoriumSummon")]
    public async Task<ScriptoriumResponse> Summon(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/summon")] HttpRequest request, CancellationToken cancellationToken)
        => (await Refusal(request, cancellationToken)).Match(
            Some: refused => new ScriptoriumResponse { Result = refused },
            None: () => new ScriptoriumResponse { Result = BackToPage("summon"), RefillReason = Summoned });

    [Function("ScriptoriumAugury")]
    public async Task<IActionResult> LetTheAuguryDecide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/augury")] HttpRequest request, CancellationToken cancellationToken)
        => await (await Refusal(request, cancellationToken)).Match(
            Some: refused => Task.FromResult(refused),
            None: async () => Answer(request, "augury", None, await scribes.LetTheAuguryDecide(cancellationToken)).Result);

    private async Task<ScriptoriumResponse> Act(HttpRequest request, Guid draftId, string action, CancellationToken cancellationToken)
    {
        var form = await ReadForm(request, cancellationToken);
        if (form.IsNone)
            return new ScriptoriumResponse { Result = new BadRequestResult() };
        var note = form.Map(fields => fields["note"].ToString()).IfNone(string.Empty);
        return action switch
        {
            "anoint" => Answer(request, action, draftId, await scribes.Anoint(draftId, note, cancellationToken)),
            "exalt" => Answer(request, action, draftId, await scribes.Exalt(draftId, note, cancellationToken)),
            "humble" => Answer(request, action, draftId, await scribes.Humble(draftId, note, cancellationToken)),
            "restore" => Answer(request, action, draftId, await scribes.Restore(draftId, note, cancellationToken)),
            "burn" => (await scribes.Burn(draftId, note, cancellationToken)).Match(
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

    // Turned off, the Scriptorium doesn't exist. Only a Scribe may act (IScribeSignIn), and a form posted from another
    // site (a forged request riding a Scribe's sign-in or address) is forbidden. Browsers say where a request comes from
    // in Sec-Fetch-Site; ones too old for that still send Origin with a form post, which must then be this site. A
    // request with neither is refused too.
    private async Task<Option<IActionResult>> Refusal(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return new NotFoundResult();
        if (!await signIn.IsScribe(request, cancellationToken) || !IsFromThisSite(request))
            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        return None;
    }

    private static bool IsFromThisSite(HttpRequest request)
    {
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0)
            return fetchSite == "same-origin";
        return Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
            && string.Equals(origin.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }

    private ScriptoriumResponse Answer(HttpRequest request, string action, Option<Guid> draftId, Either<Error, Unit> result)
        => result.Match(
            Right: _ => new ScriptoriumResponse { Result = Back(action, draftId) },
            Left: error => Failure(request, error, draftId));

    private ScriptoriumResponse Failure(HttpRequest request, Error error, Option<Guid> draftId)
    {
        if (error == ScriptoriumRepository.ChangedMeanwhile)
            return new ScriptoriumResponse { Result = Back("changed", draftId) };
        if (error == Scribes.NothingToMove)
            return new ScriptoriumResponse { Result = Back("unmoved", draftId) };
        if (error == Scribes.NoteTooLong)
            return new ScriptoriumResponse { Result = Back("note-too-long", draftId) };
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
