using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The public pages, rendered on the server: Today (<c>/</c>), the archive (<c>/archive</c>) and a rite's own page
/// (<c>/r/&lt;number&gt;</c>). Any other address gets the 404 page.
/// </summary>
public sealed class SitePages
{
    private readonly IRiteRepository rites;
    private readonly TimeProvider time;
    private readonly ILogger<SitePages> logger;

    public SitePages(IRiteRepository rites, TimeProvider time, ILogger<SitePages> logger)
    {
        this.rites = rites;
        this.time = time;
        this.logger = logger;
    }

    // The catch-all route also takes "/": more specific routes (healthz, archive, p/…) win over it.
    [Function("Today")]
    public async Task<IActionResult> Today(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*path}")] HttpRequest request,
        string? path,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(path))
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(path));

        var now = time.GetUtcNow();
        return (await rites.GetNewest(1, cancellationToken)).Match(
            Right: newest => newest.HeadOrNone().Match(
                Some: rite => Page(request, StatusCodes.Status200OK, TodayPage.Render(rite, now)),
                None: () => Page(request, StatusCodes.Status200OK, TodayPage.Slumbering(now))),
            Left: error => Failed(request, error));
    }

    [Function("Rite")]
    public async Task<IActionResult> Rite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "r/{id}")] HttpRequest request,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var number) || number < 1)
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(id));

        var found = await rites.GetByNumber(number, cancellationToken);
        return await found.Match<Task<IActionResult>>(
            Right: rite => rite.Match<Task<IActionResult>>(
                Some: async shown => Page(request, StatusCodes.Status200OK, RitePage.Render(shown, await MoreRites(shown, cancellationToken))),
                None: () => Task.FromResult<IActionResult>(Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(id)))),
            Left: error => Task.FromResult(Failed(request, error)));
    }

    /// <summary>
    /// Pages by number, newest first: <c>?before=&lt;number&gt;</c> starts below that number. The first page leaves
    /// out the newest rite, which Today shows: only the day's own rite is ever published, so the highest number is also
    /// the newest date. One rite more than a page is read, to know whether there are older ones.
    /// </summary>
    [Function("Archive")]
    public async Task<IActionResult> Archive(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var isFirstPage = !int.TryParse(request.Query["before"], out var before) || before < 1;
        var page = isFirstPage
            ? (await rites.GetOlderThan(int.MaxValue, ArchivePage.PageSize + 2, cancellationToken)).Map(found => found.Skip(1).ToList())
            : await rites.GetOlderThan(before, ArchivePage.PageSize + 1, cancellationToken);
        return page.Match(
            Right: found =>
            {
                var shown = found.Take(ArchivePage.PageSize).ToList();
                var olderThan = found.Count > ArchivePage.PageSize ? Some(shown[^1].Number) : Option<int>.None;
                return Page(request, StatusCodes.Status200OK, ArchivePage.Render(shown, olderThan));
            },
            Left: error => Failed(request, error));
    }

    // The rites closest to this one by their scores, topped up with the newest others when there aren't enough scored
    // ones (a rite Jev couldn't score, or a young site). A nicety: if it fails, the page simply goes without.
    private async Task<List<Rite>> MoreRites(Rite rite, CancellationToken cancellationToken)
    {
        var related = await rite.Similarity.Match(
                Some: similarity => rites.GetScoresByRiteNumber(similarity.ScoresGeneratorVersion, cancellationToken)
                    .MapAsync(scores => RelatedRites.Closest(rite.Number, similarity.Scores, scores)),
                None: () => Task.FromResult(Right<Error, List<int>>([])))
            .BindAsync(async closest => closest.Count >= RelatedRites.Shown
                ? Right<Error, List<int>>(closest)
                : (await rites.GetNewest(RelatedRites.Shown + 1, cancellationToken)).Map(newest => closest
                    .Concat(newest.Select(other => other.Number).Where(other => other != rite.Number && !closest.Contains(other)))
                    .Take(RelatedRites.Shown)
                    .ToList()))
            .BindAsync(numbers => rites.GetByNumbers(numbers, cancellationToken)
                .MapAsync(found => numbers.SelectMany(n => found.Where(other => other.Number == n)).ToList()));
        return related.Match(
            Right: shown => shown,
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "Rite NO. {Number} is shown without More rites: {Reason}", rite.Number, error.Message);
                return [];
            });
    }

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The page {Path} couldn't be served: {Reason}", request.Path.Value, error.Message);
        return Page(request, StatusCodes.Status503ServiceUnavailable, StatePages.Failed($"{request.Path}{request.QueryString}"));
    }

    private static ContentResult Page(HttpRequest request, int status, string html)
    {
        var headers = request.HttpContext.Response.Headers;
        // A rite never changes once published, and Today changes once a day: a minute's cache costs nothing.
        headers.CacheControl = status == StatusCodes.Status200OK ? "public, max-age=60" : "no-store";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}
