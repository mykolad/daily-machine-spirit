using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class SitePagesTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 10, 9);

    private readonly CosmosTestContainer cosmos = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 9, 20, 48, 0, TimeSpan.Zero));

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task Today_BeforeTheFirstRite_SaysTheMachineSpiritSlumbers()
    {
        var (status, html) = await Today("");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("The Machine Spirit slumbers.", html);
        Assert.Contains("3h 12m", html);
    }

    // Inline, so the icon costs no request, and browsers don't ask for /favicon.ico (which would get the 404 page).
    [Fact]
    public async Task EveryPage_CarriesItsIcon_Inline()
    {
        var (_, today) = await Today("");
        var (_, notFound) = await Today("no-such-page");

        Assert.All(new[] { today, notFound }, html => Assert.Contains("""<link rel="icon" type="image/svg+xml" href="data:image/svg+xml,%3Csvg""", html));
    }

    [Fact]
    public async Task Today_ShowsTheNewestRite_WithItsTruthSealed()
    {
        await Add(Day.AddDays(-1), "Litany of the Clean Cache");
        await Add(Day, "The Rite of Re-Run");

        var (status, html) = await Today("");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("""<h2 class="rite-title" id="rite-title">The Rite of Re-Run</h2>""", html);
        Assert.Contains("A RITUAL · NO. 2", html);
        Assert.Contains("""<time class="rite-date" datetime="2026-10-09">9 October 2026</time>""", html);
        Assert.Contains("""<section class="truth" id="truth-panel" aria-labelledby="truth-heading" hidden>""", html);
        Assert.Contains("Your test depends on <code>timing</code>.", html);
        Assert.Contains("Next litany in", html);
        Assert.Contains("""<span>Blessed</span><span class="count">0</span>""", html);
        Assert.DoesNotContain("Copy text", html);
        Assert.DoesNotContain("Filed under", html);
    }

    [Fact]
    public async Task Today_OffersTheFourRitesBeforeIt_NewestFirst()
    {
        for (var daysAgo = 5; daysAgo >= 0; daysAgo--)
        {
            await Add(Day.AddDays(-daysAgo), $"Rite {6 - daysAgo}");
        }

        var (_, html) = await Today("");

        Assert.Contains("""<h2 class="rite-title" id="rite-title">Rite 6</h2>""", html);
        var recent = Regex.Matches(html, """<a class="recent-card" href="(/r/\d+)">\s*<span class="recent-kicker">([^<]+)</span>\s*<span class="recent-title">([^<]+)</span>""")
            .Select(card => $"{card.Groups[1].Value} {card.Groups[2].Value} {card.Groups[3].Value}");
        Assert.Equal(
            ["/r/5 A RITUAL · 8 OCT Rite 5", "/r/4 A RITUAL · 7 OCT Rite 4", "/r/3 A RITUAL · 6 OCT Rite 3", "/r/2 A RITUAL · 5 OCT Rite 2"],
            recent);
    }

    [Fact]
    public async Task Today_OnTheFirstDay_HasNoRecentRites()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, html) = await Today("");

        Assert.DoesNotContain("Recent rites", html);
    }

    [Fact]
    public async Task AnyOtherAddress_IsTheNotFoundPage()
    {
        var (status, html) = await Today("nowhere");

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Contains("There is no litany with the id “nowhere”.", html);
    }

    [Fact]
    public async Task Rite_ShowsItOnItsOwnPage_AsTheTitleOfThePage()
    {
        await Add(Day, "The Rite of Re-Run");

        var (status, html) = await Rite("1");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("<title>The Rite of Re-Run · The Daily Machine Spirit</title>", html);
        Assert.Contains("""<h1 class="rite-title" id="rite-title">The Rite of Re-Run</h1>""", html);
        Assert.Contains("Back to the Archive", html);
        Assert.Contains("Copy text", html);
        Assert.DoesNotContain("Next litany in", html);
        Assert.DoesNotContain("More rites", html);
    }

    // The page is the same for everyone: both ways are in it, hidden, and the script shows the reader's own.
    [Theory]
    [InlineData("rite", "%2Fr%2F1")]
    [InlineData("today", "%2F")]
    public async Task ARitesVotes_OfferSigningInAndOut_BackToTheSamePage(string page, string returnTo)
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, html) = page == "rite" ? await Rite("1") : await Today("");

        Assert.Contains($"""<div class="vote-status" data-signed-in="false" hidden><a href="/auth/github?return={returnTo}">Sign in with GitHub</a> to vote<span class="vote-privacy">""", html);
        Assert.Contains($"""<form method="post" action="/auth/signout?return={returnTo}"><button type="submit" class="text-button">Sign out</button></form>""", html);
    }

    [Fact]
    public async Task Rite_OffersTheClosestRitesByTheirScores()
    {
        string[] titles = ["Cache A", "Builds B", "Cache C", "Cache D", "Builds E", "Cache F"];
        float[][] scores = [[1f, 0f], [0f, 1f], [0.9f, 0.1f], [0.8f, 0.2f], [0f, 1f], [0.95f, 0.05f]];
        for (var i = 0; i < titles.Length; i++)
        {
            await Add(Day.AddDays(i - titles.Length), titles[i]);
            Ok(await Repository.SaveScores(Day.AddDays(i - titles.Length),
                new RiteSimilarity { ScoresGeneratorVersion = "jev/q1", Scores = scores[i], CreatedAtUtc = DateTime.UtcNow },
                CancellationToken.None));
        }

        var (_, html) = await Rite("1");

        Assert.Equal(["Cache F", "Cache C", "Cache D"], MoreTitles(html));
    }

    [Fact]
    public async Task Rite_WhenMoreRitesFail_IsStillServed_WithoutThem()
    {
        await Add(Day.AddDays(-1), "Litany of the Clean Cache");
        await Add(Day, "The Rite of Re-Run");
        Ok(await Repository.SaveScores(Day,
            new RiteSimilarity { ScoresGeneratorVersion = "jev/q1", Scores = [1f, 0f], CreatedAtUtc = DateTime.UtcNow },
            CancellationToken.None));
        var failing = new RitesWithoutScores(Repository);
        var pages = new SitePages(failing, time, NullLogger<SitePages>.Instance);

        var page = await Read(context => pages.Rite(context.Request, "2", CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, page.Status);
        Assert.Contains("The Rite of Re-Run", page.Html);
        Assert.DoesNotContain("More rites", page.Html);
    }

    [Fact]
    public async Task Rite_WithoutScores_OffersTheNewestOtherRites()
    {
        for (var daysAgo = 4; daysAgo >= 0; daysAgo--)
        {
            await Add(Day.AddDays(-daysAgo), $"Rite {5 - daysAgo}");
        }

        var (_, html) = await Rite("4");

        Assert.Equal(["Rite 5", "Rite 3", "Rite 2"], MoreTitles(html));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("seven")]
    public async Task Rite_ThatDoesNotExist_IsNotFound(string id)
    {
        await Add(Day, "The Rite of Re-Run");

        var (status, html) = await Rite(id);

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Contains($"There is no litany with the id “{id}”.", html);
    }

    [Fact]
    public async Task Archive_LeavesOutTodaysRite_AndPagesOlderOnes()
    {
        await AddRites(23);

        var (status, first) = await Archive("");
        var (_, second) = await Archive("3");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.DoesNotContain(">Rite 23<", first);
        Assert.Equal(Enumerable.Range(3, 20).Reverse().Select(n => $"Rite {n}"), Titles(first));
        Assert.Contains("""href="/archive?before=3">Load older rites""", first);
        Assert.Equal(["Rite 2", "Rite 1"], Titles(second));
        Assert.Contains("The archive ends here.", second);
    }

    // The rows sit under a rubric per month (the year in Roman numerals, said as a number); the margins count the rites
    // and index the months, each link starting the Archive at its month.
    [Fact]
    public async Task Archive_GroupsItsRitesByMonth_CountsThem_AndIndexesTheMonths()
    {
        // 17 September to 9 October: Today's is the 23rd, the Archive's newest the 22nd.
        await AddRites(23);

        var (_, html) = await Archive("");

        Assert.Contains("""<h2 class="month-rubric" id="m-2026-10"><span>October <span aria-hidden="true">MMXXVI</span><span class="sr-only">2026</span></span></h2>""", html);
        Assert.Contains("""<h2 class="month-rubric" id="m-2026-09">""", html);
        Assert.Contains("""<p class="margin-number">23</p>""", html);
        Assert.Contains("17 September 2026", html);
        Assert.Contains("""<span class="sub-narrow">22 rites, newest first.</span>""", html);
        Assert.Contains("""<a href="/archive#m-2026-10"><span>October 2026</span><span class="month-count">8<span class="sr-only"> rites</span></span></a>""", html);
        Assert.Contains("""<a href="/archive?before=15#m-2026-09"><span>September 2026</span><span class="month-count">14<span class="sr-only"> rites</span></span></a>""", html);
        Assert.Contains("""<p class="site-name">The Daily Machine Spirit</p>""", html);
        Assert.Contains("""<h1 class="sheet-title">The Archive</h1>""", html);
    }

    // Its own field, a plain form; the nav's search icon leads to it, and there's no Search tab.
    [Fact]
    public async Task Archive_HasItsOwnSearchField()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, html) = await Archive("");

        Assert.Contains("""<form class="sheet-search" role="search" action="/archive" method="get">""", html);
        Assert.Contains("""name="q" value="" aria-label="Search the Archive" placeholder="Search the Archive" """, html);
        Assert.DoesNotContain("Clear search", html);
        Assert.Contains("""<a class="nav-search" href="/archive#archive-search" aria-label="Search">""", html);
        Assert.DoesNotContain(">Search</a>", html);
    }

    // Each count only when there is one: no line at all for a rite nobody has voted on.
    [Fact]
    public async Task Archive_ShowsEachRitesVotes_OnlyThoseCast()
    {
        await Add(Day.AddDays(-2), "The Unvoted Vigil");
        await Add(Day.AddDays(-1), "Litany of the Clean Cache");
        await Add(Day, "The Rite of Re-Run");
        var votes = new VoteRepository(cosmos.Container);
        foreach (var (voter, vote) in new[] { ("a", Reaction.Blessed), ("b", Reaction.Blessed), ("c", Reaction.Blessed) })
        {
            Ok(await votes.Vote(2, voter, Some(vote), CancellationToken.None));
        }

        var (_, html) = await Archive("");

        Assert.Contains("Blessed 3</span></span>", html);
        Assert.DoesNotContain("Heresy 0", html);
        Assert.Single(Regex.Matches(html, """<span class="row-counts">"""));
    }

    [Fact]
    public async Task Archive_WithoutRites_EndsAtOnce()
    {
        var (status, html) = await Archive("");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(Titles(html));
        Assert.Contains("The archive ends here.", html);
    }

    [Fact]
    public async Task ARitesTags_LinkToTheirPages_FromToday_ItsOwnPage_AndTheArchive()
    {
        await Add(Day.AddDays(-1), "Litany of the Clean Cache");
        await Add(Day, "The Rite of Re-Run");
        await Tag(Day.AddDays(-1), "caches", "restarts");
        await Tag(Day, "caches", "restarts");

        var (_, today) = await Today("");
        var (_, own) = await Rite("2");
        var (_, archive) = await Archive("");

        const string links = """<a class="tag" href="/tags/caches">Caches</a> <a class="tag" href="/tags/restarts">Restarts</a>""";
        Assert.Contains($"""<p class="tags"><span class="margin-label">Filed under</span> {links}</p>""", today);
        Assert.Contains($"""<p class="tags"><span class="margin-label">Filed under</span> {links}</p>""", own);
        Assert.Contains($"""<span class="row-tags"><span class="sr-only">Filed under </span>{links}</span>""", archive);
    }

    [Fact]
    public async Task Tag_ListsItsRites_NewestFirst_TodaysToo_APageAtATime()
    {
        for (var i = 1; i <= 23; i++)
        {
            await Add(Day.AddDays(i - 23), $"Rite {i}");
            await Tag(Day.AddDays(i - 23), i == 5 ? "builds" : "caches");
        }

        var (status, first) = await Tagged("caches", "");
        var (_, second) = await Tagged("caches", "3");
        var (_, beyond) = await Tagged("caches", "1");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("""<p class="sheet-kicker">Filed under</p>""", first);
        Assert.Contains("""<h1 class="sheet-title">Caches</h1>""", first);
        Assert.Contains("Rites about caches, temporary files, clean installs, leftover state, newest first.", first);
        Assert.Equal(Enumerable.Range(3, 21).Reverse().Where(n => n != 5).Select(n => $"Rite {n}"), Titles(first));
        Assert.Contains("""<a class="button button-outline" href="/tags/caches?before=3">Load older rites</a>""", first);
        Assert.Equal(["Rite 2", "Rite 1"], Titles(second));
        Assert.Contains("No earlier rite is filed under Caches.", second);
        Assert.Empty(Titles(beyond));
        Assert.Contains("No earlier rite is filed under Caches.", beyond);
    }

    [Fact]
    public async Task Tag_WithoutRites_SaysSo_AndOffersTheOtherSubjects()
    {
        await Add(Day, "The Rite of Re-Run");
        await Tag(Day, "caches");

        var (status, html) = await Tagged("security", "");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(Titles(html));
        Assert.Contains("No rite is filed under Security yet.", html);
        Assert.Contains("""<a class="tag" href="/tags/caches">Caches</a>""", html);
        Assert.DoesNotContain("""href="/tags/security""", html);
    }

    [Theory]
    [InlineData("other")]
    [InlineData("Caches")]
    [InlineData("no-such-tag")]
    public async Task Tag_ThatIsntOne_IsTheNotFoundPage(string key)
    {
        var (status, html) = await Tagged(key, "");

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Contains("<h1>No such subject.</h1>", html);
        Assert.Contains($"No rite is filed under “{key}”.", html);
    }

    // Search is the Archive's now: links to the old page still arrive, with the words.
    [Theory]
    [InlineData("?q=Re-run%20thrice", "/archive?q=Re-run%20thrice")]
    [InlineData("?q=", "/archive")]
    [InlineData("", "/archive")]
    public void Search_LeadsToTheArchive_ForGood(string query, string archive)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/search";
        context.Request.QueryString = new QueryString(query);

        var redirect = Assert.IsType<RedirectResult>(Pages().Search(context.Request));

        Assert.True(redirect.Permanent);
        Assert.Equal(archive, redirect.Url);
    }

    [Fact]
    public async Task Archive_WithWords_ListsTheRitesHoldingThem_NewestFirst_TodaysToo_AndSaysHowMany()
    {
        await AddRites(22);
        await Add(Day.AddDays(1), "The Rite of the Warm Cache");

        var (status, first) = await Searched("Re-run thrice", "");
        var (_, second) = await Searched("Re-run thrice", "4");
        var (_, single) = await Searched("cache", "");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("<title>“Re-run thrice”: 23 rites · The Daily Machine Spirit</title>", first);
        Assert.Contains("""<h1 class="sheet-title">“Re-run thrice”</h1>""", first);
        Assert.Contains("""<p class="sheet-sub">23 rites mention it, newest first.</p>""", first);
        Assert.Equal(["The Rite of the Warm Cache", .. Enumerable.Range(4, 19).Reverse().Select(n => $"Rite {n}")], Titles(first));
        Assert.Contains("""href="/archive?q=Re-run%20thrice&amp;before=4">More results</a>""", first);
        Assert.Contains("""value="Re-run thrice" """, first);
        Assert.Contains("""<a class="sheet-clear" href="/archive" aria-label="Clear search">""", first);
        Assert.Contains("Press <mark>Re-run</mark> <mark>thrice</mark>, intoning it passed locally", first);
        Assert.Equal(["Rite 3", "Rite 2", "Rite 1"], Titles(second));
        Assert.Contains("No more rites mention it.", second);
        Assert.Contains("""<p class="sheet-sub">1 rite mentions it.</p>""", single);
        Assert.Contains("The Rite of the Warm <mark>Cache</mark>", single);
        Assert.Contains("""<a href="/archive?q=cache#m-2026-10">""", single);
    }

    // A rite found by its Heretical Truth alone shows the words around them there, so the reader sees why.
    [Fact]
    public async Task Archive_WithWordsOnlyInATruth_ShowsTheTruthsWords()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, html) = await Searched("timing", "");

        Assert.Contains("""<span class="row-line"><span class="row-truth">Heretical Truth:</span> Your test depends on <mark>timing</mark>.</span>""", html);
    }

    [Fact]
    public async Task Archive_WithWordsFoundNowhere_SaysSo_AndEncodesWhatWasTyped()
    {
        await Add(Day, "The Rite of Re-Run");

        var (status, html) = await Searched("<b>nowhere</b>", "");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("""<h1 class="sheet-title">“&lt;b&gt;nowhere&lt;/b&gt;”</h1>""", html);
        Assert.Contains("No rite mentions it.", html);
        Assert.Contains("The Machine Spirit keeps its silence.", html);
        Assert.Contains("""<a class="sheet-button" href="/archive">Browse the whole Archive</a>""", html);
        Assert.Contains("""value="&lt;b&gt;nowhere&lt;/b&gt;" """, html);
        Assert.DoesNotContain("<b>nowhere</b>", html);
        Assert.DoesNotContain("""<aside class="month-index" """, html);
        Assert.Empty(Titles(html));
    }

    [Fact]
    public async Task EveryPage_WhenTheDataCantBeRead_FailsWith503_AndOffersToTryAgain()
    {
        var missing = new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), NullLogger<RiteRepository>.Instance);
        var pages = new SitePages(missing, time, NullLogger<SitePages>.Instance);

        var today = await Read(context => pages.Today(context.Request, "", CancellationToken.None));
        var rite = await Read(context => pages.Rite(context.Request, "1", CancellationToken.None));
        var archive = await Read(context => pages.Archive(context.Request, CancellationToken.None), "/archive", "?before=4");

        var tag = await Read(context => pages.Tag(context.Request, "caches", CancellationToken.None), "/tags/caches", "");

        var search = await Read(context => pages.Archive(context.Request, CancellationToken.None), "/archive", "?q=cache");

        Assert.All([today, rite, archive, tag, search], page => Assert.Equal(StatusCodes.Status503ServiceUnavailable, page.Status));
        Assert.Contains("The rite has failed.", today.Html);
        Assert.Contains("""href="/archive?before=4">""", archive.Html);
        Assert.Equal("no-store", archive.CacheControl);
    }

    [Fact]
    public async Task Pages_EncodeWhatTheModelWrote()
    {
        await Add(Day, "<b>Bold</b> & `code`");

        var page = await Read(context => Pages().Today(context.Request, "", CancellationToken.None));

        Assert.Contains("&lt;b&gt;Bold&lt;/b&gt; &amp; `code`", page.Html);
        Assert.DoesNotContain("<b>Bold</b>", page.Html);
    }

    // Votes and publishing purge Today and a rite's page from Cloudflare, so it can keep them an hour; nothing purges the
    // archive, a tag's page, or an address with a query string, which Cloudflare caches apart. Browsers keep each a
    // minute.
    [Fact]
    public async Task TodayAndARitesPage_StayAtTheEdgeUntilPurged_TheArchiveTagsAndQueriesBriefly()
    {
        var slumbering = await Read(context => Pages().Today(context.Request, "", CancellationToken.None));
        await Add(Day, "Litany of the Clean Cache");

        var today = await Read(context => Pages().Today(context.Request, "", CancellationToken.None));
        var rite = await Read(context => Pages().Rite(context.Request, "1", CancellationToken.None));
        var archive = await Read(context => Pages().Archive(context.Request, CancellationToken.None), "/archive", "");
        var withQuery = await Read(context => Pages().Today(context.Request, "", CancellationToken.None), "/", "?utm_source=feed");
        var tag = await Read(context => Pages().Tag(context.Request, "caches", CancellationToken.None), "/tags/caches", "");

        Assert.All([slumbering, today, rite], page => Assert.Equal("public, max-age=60, s-maxage=3600", page.CacheControl));
        Assert.All([archive, tag, withQuery], page => Assert.Equal("public, max-age=60", page.CacheControl));
    }

    // The script shows the counts a vote answered over a page whose counts were read before that vote.
    [Fact]
    public async Task ARitesCounts_SayWhenTheyWereRead()
    {
        await Add(Day, "Litany of the Clean Cache");

        var (_, today) = await Today("");
        var (_, rite) = await Rite("1");

        Assert.All(new[] { today, rite }, html => Assert.Contains($"""data-counted-at="{time.GetUtcNow().ToUnixTimeMilliseconds()}">""", html));
    }

    private SitePages Pages() => new(Repository, time, NullLogger<SitePages>.Instance);

    private RiteRepository Repository => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private async Task<(int Status, string Html)> Today(string path)
    {
        var page = await Read(context => Pages().Today(context.Request, path, CancellationToken.None));
        return (page.Status, page.Html);
    }

    private async Task<(int Status, string Html)> Rite(string id)
    {
        var page = await Read(context => Pages().Rite(context.Request, id, CancellationToken.None));
        return (page.Status, page.Html);
    }

    private async Task<(int Status, string Html)> Archive(string before)
    {
        var query = before.Length == 0 ? "" : $"?before={before}";
        var page = await Read(context => Pages().Archive(context.Request, CancellationToken.None), "/archive", query);
        return (page.Status, page.Html);
    }

    private async Task<(int Status, string Html)> Tagged(string key, string before)
    {
        var query = before.Length == 0 ? "" : $"?before={before}";
        var page = await Read(context => Pages().Tag(context.Request, key, CancellationToken.None), $"/tags/{key}", query);
        return (page.Status, page.Html);
    }

    private async Task<(int Status, string Html)> Searched(string words, string before)
    {
        var query = QueryString.Create(before.Length == 0 ? [new("q", words)] : new KeyValuePair<string, string?>[] { new("q", words), new("before", before) });
        var page = await Read(context => Pages().Archive(context.Request, CancellationToken.None), "/archive", query.Value ?? "");
        return (page.Status, page.Html);
    }

    private static Task<(int Status, string Html, string CacheControl)> Read(Func<HttpContext, Task<IActionResult>> serve)
        => Read(serve, "/", "");

    private static async Task<(int Status, string Html, string CacheControl)> Read(
        Func<HttpContext, Task<IActionResult>> serve, string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        var result = Assert.IsType<ContentResult>(await serve(context));
        return (result.StatusCode ?? 0, result.Content ?? "", context.Response.Headers.CacheControl.ToString());
    }

    // The More rites cards' titles, in order.
    private static List<string> MoreTitles(string html)
        => html.Split("""<span class="more-title">""").Skip(1).Select(part => part[..part.IndexOf("</span>", StringComparison.Ordinal)]).ToList();

    // The archive rows' titles, in order, without a search's marks.
    private static List<string> Titles(string html)
        => Regex.Matches(html, """<a class="row-title" href="/r/\d+">(.*?)</a>""")
            .Select(match => match.Groups[1].Value.Replace("<mark>", string.Empty).Replace("</mark>", string.Empty))
            .ToList();

    // "Rite 1" to "Rite <count>", a day apart, the last on Day: Today's.
    private async Task AddRites(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            await Add(Day.AddDays(i - count), $"Rite {i}");
        }
    }

    private async Task Tag(DateOnly day, params string[] tags)
        => Ok(await Repository.SaveTags(day, tags, CancellationToken.None));

    private async Task Add(DateOnly day, string title)
        => Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting
            {
                Kind = RiteKind.Ritual,
                Title = title,
                Text = "Press Re-run thrice, intoning `it passed locally`, for the Machine Spirit rewards the persistent.",
                HereticalTruth = "Your test depends on `timing`.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            },
        }, CancellationToken.None));
}
