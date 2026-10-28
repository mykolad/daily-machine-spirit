using System.Globalization;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Tags;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The public pages, rendered on the server: Today (<c>/</c>), the archive (<c>/archive</c>), a rite's own page
/// (<c>/r/&lt;number&gt;</c>) and a tag's (<c>/tags/&lt;key&gt;</c>); search is the archive's (<c>/archive?q=</c>).
/// Any other address gets the 404 page.
/// </summary>
public sealed class SitePages
{
    public const string CatchAllName = "ZzToday";

    private readonly IRiteReader rites;
    private readonly IOptions<PublicPagesOptions> options;
    private readonly TimeProvider time;
    private readonly ILogger<SitePages> logger;

    public SitePages(IRiteReader rites, IOptions<PublicPagesOptions> options, TimeProvider time, ILogger<SitePages> logger)
    {
        this.rites = rites;
        this.options = options;
        this.time = time;
        this.logger = logger;
    }

    // The catch-all route also takes "/". The Functions host tries the routes in the order of their functions' names and
    // takes the first that matches, not the most specific, so this one's name sorts after every other: named "Today", it
    // took /api/votes from Votes. FunctionRoutesTests keeps it last.
    [Function(CatchAllName)]
    public async Task<IActionResult> Today(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*path}")] HttpRequest request,
        string? path,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(path))
        {
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(path));
        }

        // Today's rite and the ones before it, in one read. A new rite purges Today, as does every vote, which may move its
        // relic badge.
        var now = time.GetUtcNow();
        var places = Places(cancellationToken);
        var newest = await rites.GetNewest(1 + options.Value.RecentRitesOnToday, cancellationToken);
        var placed = await places;
        return newest.Match(
            Right: found => found.HeadOrNone().Match(
                Some: rite => Page(request, StatusCodes.Status200OK, TodayPage.Render(rite, found.Skip(1).ToList(), placed, now), EdgeLifetime.UntilPurged),
                None: () => Page(request, StatusCodes.Status200OK, TodayPage.Slumbering(now), EdgeLifetime.UntilPurged)),
            Left: error => Failed(request, error));
    }

    [Function("Rite")]
    public async Task<IActionResult> Rite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "r/{id}")] HttpRequest request,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(id));
        }
        return await AtItsAddress(request, Paths.Rite(number), id, async () =>
        {
            // Taken before the counts are read, so it errs toward the voter: for a vote saved during the read, the script
            // shows that vote's answer, which has the vote, rather than counts that might not.
            var now = time.GetUtcNow();
            var places = Places(cancellationToken);
            var found = (await rites.GetByNumbers([number], cancellationToken)).Map(matches => matches.HeadOrNone());
            var placed = await places;
            return await found.Match<Task<IActionResult>>(
                Right: rite => rite.Match<Task<IActionResult>>(
                    Some: async shown => Page(request, StatusCodes.Status200OK,
                        RitePage.Render(shown, await MoreRites(shown, cancellationToken), placed, now), EdgeLifetime.UntilPurged),
                    None: () => Task.FromResult<IActionResult>(Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(id)))),
                Left: error => Task.FromResult(Failed(request, error)));
        });
    }

    // Nothing to read: the page changes with a deployment, which no purge follows, and at midnight (its example date), so
    // it's kept briefly.
    [Function("About")]
    public IActionResult About([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "catechism")] HttpRequest request)
        => Page(request, StatusCodes.Status200OK, AboutPage.Render(time.GetUtcNow()));

    /// <summary>
    /// The readers' most venerated rites: every rite's counts (a few hundred numbers), ranked here, then the few kept.
    /// Kept at the edge until a vote purges it.
    /// </summary>
    [Function("Reliquary")]
    public async Task<IActionResult> Reliquary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reliquary")] HttpRequest request,
        CancellationToken cancellationToken)
        => await AtItsAddress(request, Paths.Reliquary, (request.Path.Value ?? string.Empty).TrimStart('/'), async () =>
        {
            var now = time.GetUtcNow();
            var kept = await rites.GetVotes(cancellationToken)
                .MapAsync(Veneration.MostVenerated)
                .BindAsync(numbers => rites.GetByNumbers(numbers, cancellationToken)
                    .MapAsync(found => numbers.SelectMany(number => found.Where(rite => rite.Number == number)).ToList()));
            return kept.Match(
                Right: shown => Page(request, StatusCodes.Status200OK, ReliquaryPage.Render(shown, now), EdgeLifetime.UntilPurged),
                Left: error => Failed(request, error));
        });

    /// <summary><c>/rtfm</c>, what veterans tell newcomers, for whoever types it: it leads to the Catechism.</summary>
    [Function("ReadTheManual")]
    public IActionResult ReadTheManual([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rtfm")] HttpRequest request)
        => new RedirectResult(Paths.About, permanent: true);

    /// <summary>
    /// Pages by number, newest first: <c>?before=&lt;number&gt;</c> starts below that number. The first page leaves
    /// out the newest rite, which Today shows: only the day's own rite is ever published, so the highest number is also
    /// the newest date. One rite more than a page is read, to know whether there are older ones. With <c>?q=</c>, the
    /// rites holding every word the reader typed instead, Today's included, and with <c>&amp;tag=</c> only those filed
    /// under that tag (a tag without words is the tag's own page); the words are never logged (a failure logs the path
    /// alone).
    /// </summary>
    [Function("Archive")]
    public async Task<IActionResult> Archive(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var typed = SearchWords.Typed(request.Query["q"]);
        var words = SearchWords.From(typed);
        var tag = RiteTags.Find(request.Query["tag"].ToString());
        var isFirstPage = !int.TryParse(request.Query["before"], out var before) || before < 1;
        if (words.Count == 0 && tag.Case is Tag only)
        {
            return new RedirectResult(Paths.Tag(only.Key), permanent: false);
        }
        var places = Places(cancellationToken);
        if (words.Count > 0)
        {
            var tagKey = tag.Map(filter => filter.Key);
            var sought = SearchWords.Sought(words);
            var found = await rites.FindDaysAndTags(sought, tagKey, cancellationToken)
                .BindAsync(days => rites.SearchOlderThan(sought, tagKey, isFirstPage ? int.MaxValue : before, options.Value.ArchivePageSize + 1, cancellationToken)
                    .MapAsync(page => Results(days, page)));
            var placed = await places;
            return found.Match(
                Right: results =>
                {
                    // Once per search: the pages the script loads after the first are the same search.
                    if (isFirstPage)
                    {
                        logger.CountArchiveSearch(tag.IsSome ? "words-in-a-subject" : "words");
                    }
                    return Page(request, StatusCodes.Status200OK, ArchivePage.Results(typed, words, tag, results, placed, now));
                },
                Left: error => Failed(request, error));
        }

        var archive = await rites.GetDays(cancellationToken)
            .BindAsync(async days => (await (isFirstPage
                    ? rites.GetOlderThan(int.MaxValue, options.Value.ArchivePageSize + 2, cancellationToken).MapAsync(found => found.Skip(1).ToList())
                    : rites.GetOlderThan(before, options.Value.ArchivePageSize + 1, cancellationToken)))
                .Map(page => (Days: days, Page: OnePage(page))));
        var listedPlaces = await places;
        return archive.Match(
            Right: found => Page(request, StatusCodes.Status200OK, ArchivePage.Render(found.Page.Shown, found.Page.OlderThan, found.Days, listedPlaces, now)),
            Left: error => Failed(request, error));
    }

    /// <summary>The old search page's address, kept for links to it: search is the Archive's now.</summary>
    [Function("Search")]
    public IActionResult Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "search")] HttpRequest request)
    {
        var typed = SearchWords.Typed(request.Query["q"]);
        // A link to a later page of results keeps its place.
        var archive = typed.Length == 0 ? Paths.Archive
            : int.TryParse(request.Query["before"], out var before) && before > 0 ? Paths.ArchiveSearchBefore(typed, before)
            : Paths.ArchiveSearch(typed);
        return new RedirectResult(archive, permanent: true);
    }

    /// <summary>
    /// A tag's rites, paged like the archive, Today's rite included: one query for the page, as the rites' tags are
    /// stored with them.
    /// </summary>
    [Function("Tag")]
    public Task<IActionResult> Tag(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tags/{key}")] HttpRequest request,
        string key,
        CancellationToken cancellationToken)
        => RiteTags.Find(key).Match(
            Some: tag => TaggedRites(request, tag, cancellationToken),
            None: () => Task.FromResult<IActionResult>(Page(request, StatusCodes.Status404NotFound, StatePages.NoSuchTag(key))));

    // Like the archive, a page and one rite more, to know whether there are older ones.
    private async Task<IActionResult> TaggedRites(HttpRequest request, Tag tag, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var before = int.TryParse(request.Query["before"], out var number) && number > 0 ? number : int.MaxValue;
        var places = Places(cancellationToken);
        var page = await rites.GetTaggedOlderThan(tag.Key, before, options.Value.ArchivePageSize + 1, cancellationToken);
        var placed = await places;
        return page.Match(
            Right: found =>
            {
                var (shown, olderThan) = OnePage(found);
                return Page(request, StatusCodes.Status200OK, TagPage.Render(tag, shown, olderThan, isFirstPage: before == int.MaxValue, placed, now));
            },
            Left: error => Failed(request, error));
    }

    private SearchResults Results(List<FoundRite> found, List<Rite> page)
    {
        var (shown, olderThan) = OnePage(page);
        return new SearchResults(found, shown, olderThan);
    }

    // The rites closest to this one by their scores, topped up with the newest others when there aren't enough scored
    // ones (a rite Jev couldn't judge, or a young site). A nicety: if it fails, the page simply goes without.
    private async Task<List<Rite>> MoreRites(Rite rite, CancellationToken cancellationToken)
    {
        var related = await rite.Similarity.Match(
                Some: similarity => rites.GetScoresByRiteNumber(similarity.ScoresGeneratorVersion, cancellationToken)
                    .MapAsync(scores => RelatedRites.Closest(rite.Number, similarity.Scores, scores, options.Value.MoreRitesOnARite)),
                None: () => Task.FromResult(Right<Error, List<int>>([])))
            .BindAsync(async closest => closest.Count >= options.Value.MoreRitesOnARite
                ? Right<Error, List<int>>(closest)
                : (await rites.GetNewest(options.Value.MoreRitesOnARite + 1, cancellationToken)).Map(newest => closest
                    .Concat(newest.Select(other => other.Number).Where(other => other != rite.Number && !closest.Contains(other)))
                    .Take(options.Value.MoreRitesOnARite)
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

    // Read beside the page's own rites, so it adds no wait.
    private Task<IReadOnlyDictionary<int, int>> Places(CancellationToken cancellationToken) => RelicPlaces.Read(rites, logger, cancellationToken);

    // Newest first, read one beyond a page: the extra one says there are older ones, and the next page starts below the
    // last one shown.
    private (List<Rite> Shown, Option<int> OlderThan) OnePage(IReadOnlyList<Rite> newestFirst)
    {
        var shown = newestFirst.Take(options.Value.ArchivePageSize).ToList();
        return (shown, newestFirst.Count > options.Value.ArchivePageSize ? Some(shown[^1].Number) : Option<int>.None);
    }

    // Cloudflare keeps every spelling of an address apart, and a purge (a vote, an erasure) names only one: kept at /r/01,
    // /R/1 or /Reliquary, a page would show erased words or old counts for its hour. A trailing slash is a slip worth
    // forgiving: its redirect holds no words to go stale. A query string is fine, kept only a minute (EdgeLifetime.Brief).
    private static async Task<IActionResult> AtItsAddress(HttpRequest request, string address, string asked, Func<Task<IActionResult>> page)
    {
        if (string.Equals(request.Path.Value, address + "/", StringComparison.Ordinal))
        {
            return new RedirectResult(address + request.QueryString, permanent: true);
        }
        if (!string.Equals(request.Path.Value, address, StringComparison.Ordinal))
        {
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(asked));
        }
        return await page();
    }

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The page {Path} couldn't be served: {Reason}", request.Path.Value, error.Message);
        return Page(request, StatusCodes.Status503ServiceUnavailable, StatePages.Failed($"{request.Path}{request.QueryString}"));
    }

    private static ContentResult Page(HttpRequest request, int status, string html) => Page(request, status, html, EdgeLifetime.Brief);

    // An address with a query string (/?utm_source=...) stays brief whatever its page: Cloudflare caches each one apart,
    // and purges name only the plain address.
    private static ContentResult Page(HttpRequest request, int status, string html, EdgeLifetime edge)
    {
        var headers = request.HttpContext.Response.Headers;
        if (status != StatusCodes.Status200OK)
        {
            headers.CacheControl = "no-store";
        }
        else
        {
            (request.QueryString.HasValue ? EdgeLifetime.Brief : edge).WriteTo(headers);
        }
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}
