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
/// Every rite's words, for the search field in the nav (<c>GET /api/rites</c>): the page loads it once, when the field
/// opens, and matches as the reader types, so what they type never leaves their browser until they search. Cloudflare
/// keeps it until a new rite purges it; a few hundred rites are some tens of kilobytes compressed.
/// </summary>
public sealed class RiteIndexFunction
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IRiteRepository rites;
    private readonly ILogger<RiteIndexFunction> logger;

    public RiteIndexFunction(IRiteRepository rites, ILogger<RiteIndexFunction> logger)
    {
        this.rites = rites;
        this.logger = logger;
    }

    [Function("RiteIndex")]
    public async Task<IActionResult> Index(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/rites")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var headers = request.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        return (await rites.GetWritings(cancellationToken)).Match<IActionResult>(
            Right: written =>
            {
                headers.CacheControl = EdgeLifetime.UntilPurged.CacheControl;
                return new ContentResult
                {
                    Content = JsonSerializer.Serialize(written.Select(Entry), Json),
                    ContentType = "application/json; charset=utf-8",
                    StatusCode = StatusCodes.Status200OK,
                };
            },
            Left: error =>
            {
                logger.LogError(error.ToException(), "The rites' index couldn't be served: {Reason}", error.Message);
                headers.CacheControl = "no-store";
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }

    private static RiteIndexEntry Entry(Rite rite) => new(
        rite.Number,
        Html.IsoDate(rite.PublishedOnUtc),
        rite.Writing.Kind == RiteKind.Prayer ? "prayer" : "ritual",
        rite.Writing.Title,
        rite.Writing.Text.Replace("`", string.Empty),
        rite.Writing.HereticalTruth.Replace("`", string.Empty));
}
