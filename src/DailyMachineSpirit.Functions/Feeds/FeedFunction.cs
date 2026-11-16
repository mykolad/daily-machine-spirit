using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Feeds;

/// <summary>
/// The site's feed (<c>/feed.xml</c>, <see cref="AtomFeed"/>): the newest rites, for a feed reader to follow, or an
/// automation to post each new one. Nothing about who reads it is kept. Its address is a contract with outside clients
/// (<c>docs/api.md</c>).
/// </summary>
public sealed class FeedFunction
{
    /// <summary>A month of rites: enough for a reader who looks in rarely, small enough to fetch often.</summary>
    public const int Entries = 30;

    private readonly IRiteReader rites;
    private readonly TimeProvider time;
    private readonly ILogger<FeedFunction> logger;

    public FeedFunction(IRiteReader rites, TimeProvider time, ILogger<FeedFunction> logger)
    {
        this.rites = rites;
        this.time = time;
        this.logger = logger;
    }

    // Kept at the edge until a new rite or an erasure purges it, with the search index and the rites API. Any query
    // string would be its own cache entry: one address answers for all.
    [Function("Feed")]
    public async Task<IActionResult> Feed(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "feed.xml")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.QueryString.HasValue)
        {
            return new RedirectResult(Paths.Feed, permanent: true);
        }

        var headers = request.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        return (await rites.GetNewest(Entries, cancellationToken)).Match<IActionResult>(
            Right: newest =>
            {
                EdgeLifetime.UntilPurged.WriteTo(headers);
                return new ContentResult
                {
                    Content = AtomFeed.Render(SiteOrigin.Of(request), newest, time.GetUtcNow()),
                    ContentType = "application/atom+xml; charset=utf-8",
                    StatusCode = StatusCodes.Status200OK,
                };
            },
            Left: error =>
            {
                logger.LogError(error.ToException(), "The feed couldn't be served: {Reason}", error.Message);
                headers.CacheControl = "no-store";
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }
}
