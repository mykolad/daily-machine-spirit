using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The public rites API (<c>GET /api/v1/rites</c>, #101): every published rite for clients outside the site, each
/// Heretical Truth sealed until a client asks for that rite's (<c>/api/v1/rites/{number}/heretical-truth</c>), as the
/// site keeps it until a reader reveals it. Documented in <c>docs/api.md</c> and versioned in its path, so the site's
/// internals can change and a later version can live beside this one. Read-only, without sign-in, and nothing about
/// the caller is kept. Cloudflare keeps each answer until a new rite or an erasure purges it, as it does the search
/// index.
/// </summary>
public sealed class RitesFunction
{
    // Backticks, apostrophes and emoji as they are, not escaped (`): a client searching the saved file for
    // `|| true` or "won't" finds them. It's served as JSON, never put into a page's HTML, which the escaping guards.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly IRiteReader rites;
    private readonly ILogger<RitesFunction> logger;

    public RitesFunction(IRiteReader rites, ILogger<RitesFunction> logger)
    {
        this.rites = rites;
        this.logger = logger;
    }

    [Function("RitesV1")]
    public async Task<IActionResult> Rites(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/rites")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        OpenToAll(request);
        if (WithQueryOrSlash(request, Paths.RitesApi))
        {
            return new RedirectResult(Paths.RitesApi, permanent: true);
        }
        if (!SpelledAs(request, Paths.RitesApi))
        {
            return NotFound(request);
        }

        var site = $"https://{request.Host}";
        return (await rites.GetAllUnrevealed(cancellationToken)).Match(
            Right: all => Answer(request, new PublishedRites(all.Select(rite => Published(rite, site)).ToList())),
            Left: error => Unavailable(request, error, "The rites API couldn't be served"));
    }

    [Function("HereticalTruthV1")]
    public async Task<IActionResult> HereticalTruth(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/rites/{id}/heretical-truth")] HttpRequest request,
        string id,
        CancellationToken cancellationToken)
    {
        OpenToAll(request);
        // Read as text: the worker can't bind `01` to an int, and fails the request.
        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            return NotFound(request);
        }
        if (WithQueryOrSlash(request, Paths.HereticalTruthApi(number)))
        {
            return new RedirectResult(Paths.HereticalTruthApi(number), permanent: true);
        }
        if (!SpelledAs(request, Paths.HereticalTruthApi(number)))
        {
            return NotFound(request);
        }

        return (await rites.GetHereticalTruth(number, cancellationToken)).Match(
            Right: truth => truth.Match(
                Some: words => Answer(request, new RevealedTruth(number, words)),
                // Not kept: the rite with that number may be published tomorrow, and nothing would purge this.
                None: () => NotFound(request)),
            Left: error => Unavailable(request, error, "A Heretical Truth couldn't be revealed"));
    }

    // Any site's page may read it: it's public, and takes nothing from the caller. Set before a redirect too, which a
    // browser checks before following it.
    private static void OpenToAll(HttpRequest request)
    {
        var headers = request.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.AccessControlAllowOrigin = "*";
    }

    // Cloudflare keeps every spelling of an address apart, and a purge names only the one: an answer kept at `/01/` would
    // still show words erased from `/1/`. So the words are answered at one spelling only. A query string (which a client
    // may add to dodge a cache) or a trailing slash is forgiven with a redirect, which reads nothing and holds no words
    // to go stale; any other spelling (`/01/`, another case) is not found.
    private static bool WithQueryOrSlash(HttpRequest request, string address)
        => request.QueryString.HasValue || string.Equals(request.Path.Value, address + "/", StringComparison.Ordinal);

    private static bool SpelledAs(HttpRequest request, string address)
        => string.Equals(request.Path.Value, address, StringComparison.Ordinal);

    private static ContentResult Answer<T>(HttpRequest request, T body)
    {
        EdgeLifetime.UntilPurged.WriteTo(request.HttpContext.Response.Headers);
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(body, Json),
            ContentType = "application/json; charset=utf-8",
            StatusCode = StatusCodes.Status200OK,
        };
    }

    private static IActionResult NotFound(HttpRequest request) => NeverCached(request, new StatusCodeResult(StatusCodes.Status404NotFound));

    private IActionResult Unavailable(HttpRequest request, Error error, string what)
    {
        logger.LogError(error.ToException(), "{What}: {Reason}", what, error.Message);
        return NeverCached(request, new StatusCodeResult(StatusCodes.Status503ServiceUnavailable));
    }

    private static IActionResult NeverCached(HttpRequest request, IActionResult result)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        return result;
    }

    // The words as stored, backticks too: a client shows them as it can. An ailment not written yet is empty.
    private static PublishedRite Published(UnrevealedRite rite, string site) => new(
        rite.Number,
        Html.IsoDate(rite.PublishedOnUtc),
        rite.Kind == RiteKind.Prayer ? "prayer" : "ritual",
        rite.Title,
        rite.Text,
        rite.Ailment.IfNone(string.Empty),
        RiteTags.Of(Some(rite.Tags.ToArray())).Select(tag => new RiteSubject(tag.Key, tag.Label)).ToList(),
        site + Paths.Rite(rite.Number));
}
