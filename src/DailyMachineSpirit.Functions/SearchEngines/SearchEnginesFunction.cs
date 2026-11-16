using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.SearchEngines;

/// <summary>
/// What search engines read before the pages: <c>/robots.txt</c>, which keeps them out of the Scriptorium, a reader's
/// own pages, sign-in and the APIs, and names the sitemap (<c>/sitemap.xml</c>, <see cref="Sitemap"/>).
/// </summary>
public sealed class SearchEnginesFunction
{
    private readonly IRiteReader rites;
    private readonly ILogger<SearchEnginesFunction> logger;

    public SearchEnginesFunction(IRiteReader rites, ILogger<SearchEnginesFunction> logger)
    {
        this.rites = rites;
        this.logger = logger;
    }

    // Kept at the edge until a new rite purges it (DailyRiteFunction). Any query string would be its own cache entry: one
    // address answers for all.
    [Function("Sitemap")]
    public async Task<IActionResult> Sitemap(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sitemap.xml")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.QueryString.HasValue)
        {
            return new RedirectResult(Paths.Sitemap, permanent: true);
        }

        var headers = request.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        return (await rites.GetDays(cancellationToken)).Match<IActionResult>(
            Right: days =>
            {
                EdgeLifetime.UntilPurged.WriteTo(headers);
                return new ContentResult
                {
                    Content = SearchEngines.Sitemap.Render(SiteOrigin.Of(request), days),
                    ContentType = "application/xml; charset=utf-8",
                    StatusCode = StatusCodes.Status200OK,
                };
            },
            Left: error =>
            {
                logger.LogError(error.ToException(), "The sitemap couldn't be served: {Reason}", error.Message);
                headers.CacheControl = "no-store";
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }

    // Nothing to read, and it changes only with a deployment, which no purge follows: kept briefly, like the Catechism.
    [Function("Robots")]
    public IActionResult Robots([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "robots.txt")] HttpRequest request)
    {
        var headers = request.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        EdgeLifetime.Brief.WriteTo(headers);
        return new ContentResult
        {
            Content = $"""
                User-agent: *
                Disallow: /scriptorium
                Disallow: {Paths.Breviary}
                Disallow: /auth/
                Disallow: /api/

                Sitemap: {SiteOrigin.Of(request)}{Paths.Sitemap}

                """,
            ContentType = "text/plain; charset=utf-8",
            StatusCode = StatusCodes.Status200OK,
        };
    }
}
