using DailyMachineSpirit.Data.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Drawing a rite (#196): the lots fall on one at random. The script draws in the browser, from the search index, so this
/// is only for a reader without it: a redirect to a random rite's page, never kept anywhere (a kept one would send every
/// reader to the same rite).
/// </summary>
public sealed class DrawFunction
{
    private readonly IRiteReader rites;
    private readonly Random random;
    private readonly ILogger<DrawFunction> logger;

    public DrawFunction(IRiteReader rites, Random random, ILogger<DrawFunction> logger)
    {
        this.rites = rites;
        this.random = random;
        this.logger = logger;
    }

    // Every rite's number and day is one cheap read; Cloudflare rate-limits the path, as it does search.
    [Function("Draw")]
    public async Task<IActionResult> Draw(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "draw")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        return (await rites.GetDays(cancellationToken)).Match<IActionResult>(
            // Before the first rite there's nothing to draw: Today says when there will be.
            Right: days => new RedirectResult(days.Count == 0 ? Paths.Today : Paths.Drawn(days[random.Next(days.Count)].Number), permanent: false),
            Left: error =>
            {
                logger.LogError(error.ToException(), "No rite could be drawn: {Reason}", error.Message);
                return new ContentResult
                {
                    Content = StatePages.Failed(Paths.Draw),
                    ContentType = "text/html; charset=utf-8",
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                };
            });
    }
}
