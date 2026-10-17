using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The search index (<c>GET /api/search-index</c>): not the rites, only what the nav's search field matches them on
/// and shows. The page loads it once, when the field opens, and matches as the reader types, so what they type never
/// leaves their browser until they search. Cloudflare keeps it until a new rite purges it; a few hundred rites are some
/// tens of kilobytes compressed.
/// </summary>
public sealed class SearchIndexFunction
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IRiteRepository rites;
    private readonly ILogger<SearchIndexFunction> logger;

    public SearchIndexFunction(IRiteRepository rites, ILogger<SearchIndexFunction> logger)
    {
        this.rites = rites;
        this.logger = logger;
    }

    [Function("SearchIndex")]
    public async Task<IActionResult> Index(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/search-index")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        // Cloudflare keeps an address with a query string apart, so /api/search-index?anything would read every rite
        // again. One address for all: a redirect costs no read, and it's cached too.
        if (request.QueryString.HasValue)
        {
            return new RedirectResult(Paths.SearchIndex, permanent: true);
        }

        var headers = request.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        return (await rites.GetAllWords(cancellationToken)).Match<IActionResult>(
            Right: words =>
            {
                headers.CacheControl = EdgeLifetime.UntilPurged.CacheControl;
                return new ContentResult
                {
                    Content = JsonSerializer.Serialize(words.Select(Entry), Json),
                    ContentType = "application/json; charset=utf-8",
                    StatusCode = StatusCodes.Status200OK,
                };
            },
            Left: error =>
            {
                logger.LogError(error.ToException(), "The search index couldn't be served: {Reason}", error.Message);
                headers.CacheControl = "no-store";
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }

    // The words as stored, backticks too, so the nav matches exactly what the Archive's search (CONTAINS) matches.
    private static SearchIndexEntry Entry(RiteWords rite) => new(
        rite.Number,
        Html.IsoDate(rite.PublishedOnUtc),
        rite.Kind == RiteKind.Prayer ? "prayer" : "ritual",
        rite.Title,
        rite.Text,
        rite.HereticalTruth);
}
