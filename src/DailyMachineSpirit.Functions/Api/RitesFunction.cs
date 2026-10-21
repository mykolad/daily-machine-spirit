using System.Text.Encodings.Web;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Tags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The public rites API (<c>GET /api/v1/rites</c>, #101): every published rite, for clients outside the site. Documented
/// in <c>docs/api.md</c> and versioned in its path, so the site's internals can change and a later version can live
/// beside this one. Read-only, without sign-in, and nothing about the caller is kept. Cloudflare keeps it until a new
/// rite or an erasure purges it, as it does the search index.
/// </summary>
public sealed class RitesFunction
{
    // Backticks, apostrophes and emoji as they are, not escaped (`): a client searching the saved file for
    // `|| true` or "won't" finds them. It's served as JSON, never put into a page's HTML, which the escaping guards.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly IRiteRepository rites;
    private readonly ILogger<RitesFunction> logger;

    public RitesFunction(IRiteRepository rites, ILogger<RitesFunction> logger)
    {
        this.rites = rites;
        this.logger = logger;
    }

    [Function("RitesV1")]
    public async Task<IActionResult> Rites(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/rites")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var headers = request.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        // Any site's page may read it: it's public, and takes nothing from the caller. Set before the redirect below,
        // which a browser checks too before following it.
        headers.AccessControlAllowOrigin = "*";

        // Cloudflare keeps an address with a query string apart, so every new one would read every rite again. One
        // address for all: a redirect costs no read, and it's cached too.
        if (request.QueryString.HasValue)
        {
            return new RedirectResult(Paths.RitesApi, permanent: true);
        }

        return (await rites.GetAllTaggedWords(cancellationToken)).Match<IActionResult>(
            Right: all =>
            {
                headers.CacheControl = EdgeLifetime.UntilPurged.CacheControl;
                var site = $"https://{request.Host}";
                return new ContentResult
                {
                    Content = JsonSerializer.Serialize(new PublishedRites(all.Select(rite => Published(rite, site)).ToList()), Json),
                    ContentType = "application/json; charset=utf-8",
                    StatusCode = StatusCodes.Status200OK,
                };
            },
            Left: error =>
            {
                logger.LogError(error.ToException(), "The rites API couldn't be served: {Reason}", error.Message);
                headers.CacheControl = "no-store";
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }

    // The words as stored, backticks too: a client shows them as it can.
    private static PublishedRite Published(TaggedRiteWords rite, string site) => new(
        rite.Number,
        Html.IsoDate(rite.PublishedOnUtc),
        rite.Kind == RiteKind.Prayer ? "prayer" : "ritual",
        rite.Title,
        rite.Text,
        rite.HereticalTruth,
        RiteTags.Of(Some(rite.Tags.ToArray())).Select(tag => new RiteSubject(tag.Key, tag.Label)).ToList(),
        site + Paths.Rite(rite.Number));
}
