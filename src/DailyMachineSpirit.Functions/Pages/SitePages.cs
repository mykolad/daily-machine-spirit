using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The public pages, rendered on the server: Today (<c>/</c>), the archive (<c>/archive</c>), a rite's own page
/// (<c>/r/&lt;number&gt;</c>), a tag's (<c>/tags/&lt;key&gt;</c>) and search (<c>/search</c>). Any other
/// address gets the 404 page.
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

    // The catch-all route also takes "/": more specific routes (healthz, archive, r/…) win over it.
    [Function("Today")]
    public async Task<IActionResult> Today(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*path}")] HttpRequest request,
        string? path,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(path))
        {
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(path));
        }

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
        {
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(id));
        }

        var found = (await rites.GetByNumbers([number], cancellationToken)).Map(matches => matches.HeadOrNone());
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
                var (shown, olderThan) = OnePage(found);
                return Page(request, StatusCodes.Status200OK, ArchivePage.Render(shown, olderThan));
            },
            Left: error => Failed(request, error));
    }

    /// <summary>
    /// A tag's rites, paged like the archive, Today's rite included. Which rites have the tag comes from every rite's
    /// scores (<see cref="RiteTags"/>): one small query over a few hundred rites, then the page's own rites.
    /// </summary>
    [Function("Tag")]
    public Task<IActionResult> Tag(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tags/{key}")] HttpRequest request,
        string key,
        CancellationToken cancellationToken)
        => RiteTags.Find(key).Match(
            Some: tag => TaggedRites(request, tag, cancellationToken),
            None: () => Task.FromResult<IActionResult>(Page(request, StatusCodes.Status404NotFound, StatePages.NotFound($"tags/{key}"))));

    /// <summary>
    /// The rites holding every word the reader typed (<c>?q=</c>), paged like the archive, Today's rite included. The
    /// words are never logged: a failure logs the path alone.
    /// </summary>
    [Function("Search")]
    public async Task<IActionResult> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "search")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var typed = SearchWords.Typed(request.Query["q"]);
        var words = SearchWords.From(typed);
        if (words.Count == 0)
        {
            return Page(request, StatusCodes.Status200OK, SearchPage.Render(typed, None));
        }

        var before = int.TryParse(request.Query["before"], out var number) && number > 0 ? number : int.MaxValue;
        var found = await rites.Search(words, before, ArchivePage.PageSize + 1, cancellationToken)
            .BindAsync(page => Counted(words, before, page, cancellationToken));
        return found.Match(
            Right: results => Page(request, StatusCodes.Status200OK, SearchPage.Render(typed, results)),
            Left: error => Failed(request, error));
    }

    // A first page that holds every result is its own count; only longer searches ask Cosmos to count.
    private async Task<Either<Error, SearchResults>> Counted(
        IReadOnlyList<string> words, int before, List<Rite> page, CancellationToken cancellationToken)
    {
        var (shown, olderThan) = OnePage(page);
        var count = before == int.MaxValue && olderThan.IsNone
            ? Right<Error, int>(page.Count)
            : await rites.CountFound(words, cancellationToken);
        return count.Map(found => new SearchResults(found, shown, olderThan));
    }

    // Like the archive, a page and one rite more, to know whether there are older ones.
    private async Task<IActionResult> TaggedRites(HttpRequest request, Tag tag, CancellationToken cancellationToken)
    {
        var before = int.TryParse(request.Query["before"], out var number) && number > 0 ? number : int.MaxValue;
        var page = await rites.GetSimilarities(cancellationToken)
            .MapAsync(similarities => similarities
                .Where(rite => rite.Key < before && RiteTags.Of(rite.Value).Contains(tag))
                .Select(rite => rite.Key)
                .OrderDescending()
                .Take(ArchivePage.PageSize + 1)
                .ToList())
            .BindAsync(numbers => rites.GetByNumbers(numbers, cancellationToken));
        return page.Match(
            Right: found =>
            {
                var (shown, olderThan) = OnePage(found.OrderByDescending(rite => rite.Number).ToList());
                return Page(request, StatusCodes.Status200OK, TagPage.Render(tag, shown, olderThan));
            },
            Left: error => Failed(request, error));
    }

    // The rites closest to this one by their scores, topped up with the newest others when there aren't enough scored
    // ones (a rite Jev couldn't judge, or a young site). A nicety: if it fails, the page simply goes without.
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

    // Newest first, read one beyond a page: the extra one says there are older ones, and the next page starts below the
    // last one shown.
    private static (List<Rite> Shown, Option<int> OlderThan) OnePage(IReadOnlyList<Rite> newestFirst)
    {
        var shown = newestFirst.Take(ArchivePage.PageSize).ToList();
        return (shown, newestFirst.Count > ArchivePage.PageSize ? Some(shown[^1].Number) : Option<int>.None);
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
