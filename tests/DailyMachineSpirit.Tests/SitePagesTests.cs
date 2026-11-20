using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Tags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class SitePagesTests : IAsyncLifetime
{
    private const string Site = "dailymachinespirit.test";

    private static readonly DateOnly Day = new(2026, 10, 9);

    private readonly CosmosTestContainer cosmos = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 9, 20, 48, 0, TimeSpan.Zero));

    public ValueTask InitializeAsync() => cosmos.InitializeAsync();

    public ValueTask DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task Today_BeforeTheFirstRite_SaysTheMachineSpiritSlumbers()
    {
        var (status, html) = await Today("");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("The Machine Spirit slumbers.", html);
        Assert.Contains("3h 12m", html);
    }

    // The script reloads a page shown again once its next rite is due, so every countdown says when, as of the render.
    [Fact]
    public async Task EveryCountdown_SaysWhenTheNextRiteIsDue()
    {
        var (_, slumbering) = await Today("");
        await Add(Day, "The Rite of Re-Run");
        time.Advance(TimeSpan.FromHours(4));

        var (_, today) = await Today("");
        var (_, archive) = await Archive("");

        Assert.Equal(["2026-10-10T00:00:00Z"], Countdowns(slumbering));
        Assert.Equal(["2026-10-11T00:00:00Z", "2026-10-11T00:00:00Z"], Countdowns(today));
        Assert.Equal(["2026-10-11T00:00:00Z"], Countdowns(archive));
    }

    // On Today the motto sits under the rite, where it's read, and leaves the footer, so it's said once; every other
    // page keeps it in the footer.
    [Fact]
    public async Task TheMotto_IsUnderTodaysRite_AndInTheFooterElsewhere()
    {
        await Add(Day, "The Rite of Re-Run");
        const string motto = "Knowledge is lost. The rituals remain.";

        var (_, today) = await Today("");
        var (_, notFound) = await Today("no-such-page");
        var (_, archive) = await Archive("");

        Assert.Equal(1, today.Split(motto).Length - 1);
        Assert.Contains($"""<p class="epigraph">{motto}</p>""", today);
        // The rendered elements, not the stylesheet's selectors (which come first, in the head): the rite, then the motto.
        var heading = today.IndexOf("""<h2 class="rite-title" id="rite-title">""", StringComparison.Ordinal);
        var epigraph = today.IndexOf("""<p class="epigraph">""", StringComparison.Ordinal);
        Assert.True(heading >= 0 && epigraph > heading, $"The rite's heading is at {heading}, the motto at {epigraph}.");
        Assert.Contains($"""<p class="motto">{motto}</p>""", archive);
        Assert.Contains($"""<p class="motto">{motto}</p>""", notFound);
    }

    // Cloudflare adds its timing beacon to every page on the way out, so every page says so, the app's own included.
    [Fact]
    public async Task EveryPage_SaysCloudflareMeasuresItsLoading()
    {
        await Add(Day, "The Rite of Re-Run");
        const string notice = "Cloudflare measures how fast pages load for readers, without cookies and without keeping your IP address";

        var (_, today) = await Today("");
        var (_, notFound) = await Today("no-such-page");
        var (_, archive) = await Archive("");

        Assert.All([today, notFound, archive], html => Assert.Contains(notice, html));
    }

    [Fact]
    public async Task BeforeTheFirstRite_TheMottoIsUnderTheSlumber()
    {
        var (_, html) = await Today("");

        Assert.Contains("""<p class="epigraph">Knowledge is lost. The rituals remain.</p>""", html);
        Assert.DoesNotContain("""<p class="motto">""", html);
    }

    // Inline, so the icon costs no request, and browsers don't ask for /favicon.ico (which would get the 404 page).
    [Fact]
    public async Task EveryPage_CarriesItsIcon_Inline()
    {
        var (_, today) = await Today("");
        var (_, notFound) = await Today("no-such-page");

        Assert.All(new[] { today, notFound }, html => Assert.Contains("""<link rel="icon" type="image/svg+xml" href="data:image/svg+xml,%3Csvg""", html));
    }

    // The synthetic check of Today (observability/synthetics/todays-rite.js) finds the rite's day by its own pattern: a
    // change to the rite card that leaves it blind (#124 did) fails here, not as an alert in the night.
    [Fact]
    public async Task TheSyntheticCheck_FindsTodaysRiteOnToday()
    {
        await Add(Day.AddDays(-1), "Litany of the Clean Cache");
        await Add(Day, "The Rite of Re-Run");
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "todays-rite.js"), TestContext.Current.CancellationToken);
        var pattern = Regex.Match(script, @"const shown = /(?<pattern>.+)/\.exec").Groups["pattern"].Value;

        var (_, html) = await Today("");

        Assert.NotEmpty(pattern);
        Assert.Equal(Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Regex.Match(html, pattern).Groups[1].Value);
    }

    // The daily check in GitHub (.github/workflows/check-todays-rite.yml) looks for its marker and the day after it:
    // blind, it opens an issue saying today has no rite (#134).
    [Fact]
    public async Task TheDailyCheck_FindsTodaysRiteOnToday()
    {
        await Add(Day, "The Rite of Re-Run");
        var workflow = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "check-todays-rite.yml"), TestContext.Current.CancellationToken);
        var marker = Regex.Match(workflow, "marker='(?<marker>[^']+)'").Groups["marker"].Value;

        var (_, html) = await Today("");

        Assert.NotEmpty(marker);
        Assert.Contains($"{marker}{Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}\"", html);
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
        Assert.Contains("""<span class="imperial rite-date"><button type="button" class="imperial-date" aria-describedby="rite-day"><time datetime="2026-10-09">0 769 026.M3</time></button><span class="imperial-day" id="rite-day" role="tooltip">Friday 9 October 2026</span></span>""", html);
        Assert.Contains("""<section class="truth" id="truth-panel" aria-labelledby="truth-heading" tabindex="-1" hidden>""", html);
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
        Assert.Equal(
            new[] { 5, 4, 3, 2 }.Select(number => $"/r/{number} A RITUAL · {ImperialDate.InALink(Day.AddDays(number - 6), "parchment-card-date")} Rite {number}"),
            Parchments(html));
    }

    // More rites are the same small parchments as Today's recent rites, so the two read as one thing.
    [Fact]
    public async Task ARitesPage_OffersMoreRites_AsTodaysParchments()
    {
        for (var daysAgo = 3; daysAgo >= 0; daysAgo--)
        {
            await Add(Day.AddDays(-daysAgo), $"Rite {4 - daysAgo}");
        }

        var (_, html) = await Rite("1");

        Assert.Equal(
            new[] { 4, 3, 2 }.Select(number => $"/r/{number} A RITUAL · {ImperialDate.InALink(Day.AddDays(number - 4), "parchment-card-date")} Rite {number}"),
            Parchments(html));
        Assert.Contains("""<ul class="parchments parchments-few">""", html);
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
    public async Task Rite_WhenMoreRitesAndTheRelicPlacesFail_IsStillServed_WithoutThem()
    {
        await Add(Day.AddDays(-1), "Litany of the Clean Cache");
        await Add(Day, "The Rite of Re-Run");
        Ok(await Repository.SaveScores(Day,
            new RiteSimilarity { ScoresGeneratorVersion = "jev/q1", Scores = [1f, 0f], CreatedAtUtc = DateTime.UtcNow },
            CancellationToken.None));
        Ok(await new VoteRepository(cosmos.Container).Vote(2, new VoterKey("a"), Some(Reaction.Blessed), CancellationToken.None));
        var failing = new RitesWithoutScoresOrCounts(Repository);
        var pages = new SitePages(failing, Options.Create(Settings.PublicPages), time, NullLogger<SitePages>.Instance);

        var page = await Read(context => pages.Rite(context.Request, "2", CancellationToken.None), "/r/2", "");

        Assert.Equal(StatusCodes.Status200OK, page.Status);
        Assert.Contains("The Rite of Re-Run", page.Html);
        Assert.DoesNotContain("More rites", page.Html);
        Assert.DoesNotContain("""class="relic""", page.Html);
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

    // Cloudflare would keep each apart from /r/1, and a purge naming /r/1 would leave its old words there.
    [Theory]
    [InlineData("/r/01", "01")]
    [InlineData("/r/+1", "+1")]
    [InlineData("/R/1", "1")]
    public async Task Rite_SpelledAnotherWay_IsNotFound(string path, string id)
    {
        await Add(Day, "The Rite of Re-Run");

        var page = await Read(context => Pages().Rite(context.Request, id, CancellationToken.None), path, "");

        Assert.Equal((StatusCodes.Status404NotFound, "no-store"), (page.Status, page.CacheControl));
    }

    [Fact]
    public async Task Rite_WithATrailingSlash_RedirectsToItsAddress_KeepingTheQuery()
    {
        await Add(Day, "The Rite of Re-Run");
        var context = new DefaultHttpContext();
        context.Request.Path = "/r/1/";
        context.Request.QueryString = new QueryString("?utm_source=feed");

        var result = Assert.IsType<RedirectResult>(await Pages().Rite(context.Request, "1", CancellationToken.None));

        Assert.Equal(("/r/1?utm_source=feed", true), (result.Url, result.Permanent));
    }

    // A query string is kept a minute only (EdgeLifetime.Brief), so it may stay.
    [Fact]
    public async Task Rite_WithAQueryString_IsServed()
    {
        await Add(Day, "The Rite of Re-Run");

        var page = await Read(context => Pages().Rite(context.Request, "1", CancellationToken.None), "/r/1", "?utm_source=feed");

        Assert.Equal((StatusCodes.Status200OK, "public, max-age=60, stale-if-error=604800"), (page.Status, page.AtCloudflare));
    }

    // Every rite is in the Archive, Today's first, so its count, its months and its list agree.
    [Fact]
    public async Task Archive_ListsTodaysRiteFirst_AndPagesOlderOnes()
    {
        await AddRites(23);

        var (status, first) = await Archive("");
        var (_, second) = await Archive("4");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(Enumerable.Range(4, 20).Reverse().Select(n => $"Rite {n}"), Titles(first));
        Assert.Contains("""href="/archive?before=4">Load older rites""", first);
        Assert.Equal(["Rite 3", "Rite 2", "Rite 1"], Titles(second));
        Assert.Contains("The archive ends here.", second);
    }

    // The rows sit under a rubric per month (the year in Roman numerals, said as a number); the margins count the rites
    // and index the months, each link starting the Archive at its month.
    [Fact]
    public async Task Archive_GroupsItsRitesByMonth_CountsThem_AndIndexesTheMonths()
    {
        // 17 September to 9 October: Today's is the 23rd, and the Archive lists it too.
        await AddRites(23);

        var (_, html) = await Archive("");

        Assert.Contains("""<h2 class="month-rubric" id="m-2026-10"><span>October <span aria-hidden="true">MMXXVI</span><span class="sr-only">2026</span></span></h2>""", html);
        Assert.Contains("""<h2 class="month-rubric" id="m-2026-09">""", html);
        Assert.Contains("""<p class="margin-number">23</p>""", html);
        Assert.Contains("17 September 2026", html);
        Assert.Contains("""<span class="sub-narrow">23 rites, newest first.</span>""", html);
        Assert.Contains("""<a href="/archive#m-2026-10"><span>October 2026</span><span class="month-count">9<span class="sr-only"> rites</span></span></a>""", html);
        Assert.Contains("""<a href="/archive?before=15#m-2026-09"><span>September 2026</span><span class="month-count">14<span class="sr-only"> rites</span></span></a>""", html);
        Assert.Contains("""<p class="site-name">The Daily Machine Spirit</p>""", html);
        Assert.Contains("""<h1 class="sheet-title">The Archive</h1>""", html);
    }

    // Today's frame on the sheet: the double rule, the corners, code in the margins, hidden from screen readers; and each
    // row's number, which a screen reader hears as "Rite 2".
    [Fact]
    public async Task Archive_SheetHasTodaysFrame_AndNumbersItsRows()
    {
        await Add(Day.AddDays(-1), "Litany of the Clean Cache");
        await Add(Day, "The Rite of Re-Run");

        var (_, html) = await Archive("");

        Assert.Contains("""<span class="rule-outer" aria-hidden="true"></span><span class="rule-inner" aria-hidden="true"></span>""", html);
        Assert.Contains("""<svg class="corner corner-tl" """, html);
        Assert.Contains("""<span class="margin-text margin-left" aria-hidden="true" data-code="4E 4F 54 48 49 4E 47 20 49 53 20 46 4F 52 47 4F 54 54 45 4E" data-copies="12">4E 4F 54 48 49 4E 47 20 49 53 20 46 4F 52 47 4F 54 54 45 4E · """, html);
        Assert.Contains("""<span class="margin-text margin-right" aria-hidden="true" data-code="01001011 01000101 01010000 01010100" data-copies="12">01001011 01000101 01010000 01010100 · """, html);
        Assert.Contains("""<span class="row-number"><span aria-hidden="true">NO. </span><span class="sr-only">Rite </span>1</span>""", html);
    }

    // The seal hangs from the sheet where the Archive ends, and only there: not over a page with older rites below, nor
    // at the end of a search or a tag's rites.
    [Fact]
    public async Task Archive_EndsWithItsSeal_OnItsLastPageOnly()
    {
        await AddRites(23);
        await Tag(Day, "caches");

        var (_, first) = await Archive("");
        var (_, last) = await Archive("3");
        var (_, searched) = await Searched("Re-run", "4");
        var (_, tagged) = await Tagged("caches", "");

        Assert.DoesNotContain("""<div class="archive-seal" """, first);
        Assert.Contains("""<div class="archive-seal" aria-hidden="true">""", last);
        Assert.Contains("""<span class="purity-strip">Read by no one since</span>""", last);
        Assert.Contains("The archive ends here.", last);
        Assert.All([searched, tagged], html => Assert.DoesNotContain("""<div class="archive-seal" """, html));
    }

    // Its own field, a plain form; the nav's search icon leads to it, and there's no Search tab.
    [Fact]
    public async Task Archive_HasItsOwnSearchField()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, html) = await Archive("");

        Assert.Contains("""<form class="sheet-search" role="search" action="/archive" method="get">""", html);
        Assert.Contains("""name="q" value="" aria-label="Search the Archive" placeholder="Search the Archive" """, html);
        Assert.DoesNotContain("""aria-label="Clear search">""", html);
        Assert.Contains("""<a class="nav-search" href="/archive#archive-search" aria-label="Search">""", html);
        Assert.DoesNotContain(">Search</a>", html);
    }

    // The nav's search suggests subjects from the page itself, so typing asks the server nothing; the Archive, whose icon
    // leads to its own field, goes without.
    [Fact]
    public async Task NavSearch_CarriesEverySubject_ExceptOnTheArchive()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, today) = await Today("");
        var (_, archive) = await Archive("");

        var subjects = Regex.Match(today, "data-subjects=\"([^\"]*)\"").Groups[1].Value;
        Assert.Equal(RiteTags.All, JsonSerializer.Deserialize<List<Tag>>(WebUtility.HtmlDecode(subjects), JsonSerializerOptions.Web));
        Assert.DoesNotContain("data-subjects", archive);
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
            Ok(await votes.Vote(2, new VoterKey(voter), Some(vote), CancellationToken.None));
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
    [InlineData("?q=cache&before=4", "/archive?q=cache&before=4")]
    [InlineData("?q=cache&before=nonsense", "/archive?q=cache")]
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
    public void ReadTheManual_LeadsToTheCatechism_ForGood()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/rtfm";

        var redirect = Assert.IsType<RedirectResult>(Pages().ReadTheManual(context.Request));

        Assert.True(redirect.Permanent);
        Assert.Equal("/catechism", redirect.Url);
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

    // Narrowed to a subject, only its rites; the form carries the tag along, and the chip takes it off again.
    [Fact]
    public async Task Archive_WithWordsAndATag_ListsOnlyThatSubjectsRites()
    {
        await AddCaches();

        var (status, html) = await SearchedWithin("cache", "caches");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(["The Warm Cache"], Titles(html));
        Assert.Contains("<title>“cache” in Caches: 1 rite · The Daily Machine Spirit</title>", html);
        Assert.Contains("""<p class="sheet-sub">1 rite mentions it.</p>""", html);
        Assert.Contains("""<input type="hidden" name="tag" value="caches">""", html);
        // Said once, by the chip: not again in the margin.
        Assert.DoesNotContain("""<p class="margin-label">Filed under</p>""", html);
        Assert.Contains("""<a class="filter-chip" href="/archive?q=cache" aria-label="Remove filter: Caches">Caches""", html);
        Assert.Contains("""<h2 id="narrow">Other subjects</h2>""", html);
        Assert.Contains("""<a class="tag" href="/archive?q=cache&amp;tag=builds">Builds (1)</a>""", html);
        Assert.DoesNotContain("tag=caches\">", html);
    }

    // Without the script, the subjects the found rites are filed under narrow the search, the most first.
    [Fact]
    public async Task Archive_WithWords_OffersTheFoundRitesSubjects()
    {
        await AddCaches();

        var (_, html) = await Searched("cache", "");

        Assert.Equal(["The Cold Cache", "The Build Cache", "The Warm Cache"], Titles(html));
        Assert.Contains("""<h2 id="narrow">Narrow to a subject</h2>""", html);
        Assert.Contains("""<a class="tag" href="/archive?q=cache&amp;tag=builds">Builds (2)</a> <a class="tag" href="/archive?q=cache&amp;tag=caches">Caches (1)</a>""", html);
        Assert.DoesNotContain("""class="filter-chip" """, html);
    }

    // A word naming a subject finds the rites filed under it, its words or not, and marks the subject it named.
    [Fact]
    public async Task Archive_WithAWordNamingASubject_FindsTheRitesFiledUnderIt()
    {
        await AddCaches();

        var (_, html) = await Searched("builds", "");

        Assert.Equal(["The Build Cache", "The Warm Cache"], Titles(html));
        Assert.Contains("""<a class="tag" href="/tags/builds"><span><mark>Builds</mark></span></a>""", html);
        Assert.Contains("""<a class="tag" href="/tags/caches">Caches</a>""", html);
    }

    // The marked part and the rest stay one run of text in the chip, so the space between the name's words stays.
    [Fact]
    public async Task Archive_WithAWordNamingPartOfASubject_MarksItInTheWholeName()
    {
        await Add(Day, "The Lost Branch");
        await Tag(Day, "version-control");

        var (_, html) = await Searched("control", "");

        Assert.Contains("""<a class="tag" href="/tags/version-control"><span>Version <mark>control</mark></span></a>""", html);
    }

    // Nothing found within a subject: the way out is the same words in every subject.
    [Fact]
    public async Task Archive_WithATagNoRiteMatches_OffersEverySubject()
    {
        await AddCaches();

        var (_, html) = await SearchedWithin("cold", "caches");

        Assert.Contains("""<p class="sheet-sub">No rite mentions it.</p>""", html);
        Assert.Contains("""<a class="sheet-button" href="/archive?q=cold">Search every subject</a>""", html);
    }

    // A tag without words is the tag's own page; a tag the site doesn't have is no filter at all.
    [Fact]
    public async Task Archive_WithATagAlone_IsTheTagsPage_AndAnUnknownTagIsIgnored()
    {
        await AddCaches();
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?tag=caches");

        var redirect = Assert.IsType<RedirectResult>(await Pages().Archive(context.Request, CancellationToken.None));
        var (_, unknown) = await SearchedWithin("cache", "nonsense");

        Assert.Equal(("/tags/caches", false), (redirect.Url, redirect.Permanent));
        Assert.Equal(3, Titles(unknown).Count);
        Assert.DoesNotContain("""name="tag" """, unknown);
    }

    // A reader searches by their trouble (#197): "flaky" finds the rite for it, though the rite's own words never say so.
    [Fact]
    public async Task Archive_WithWordsOnlyInTheAilment_ListsTheRite()
    {
        await Add(Day.AddDays(-1), "The Rite of the Warm Cache");
        await Add(Day, "The Rite of Re-Run");
        await Ail(Day, "a flaky test that passes when re-run");

        var (_, html) = await Searched("flaky", "");

        Assert.Equal(["The Rite of Re-Run"], Titles(html));
    }

    // A rite found by its Heretical Truth alone is listed, but its truth stays sealed: the row shows the opening words,
    // and breaking the seal is the reader's choice, on the rite's own page.
    [Fact]
    public async Task Archive_WithWordsOnlyInATruth_ListsTheRite_WithoutRevealingTheTruth()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, html) = await Searched("timing", "");

        Assert.Equal(["The Rite of Re-Run"], Titles(html));
        Assert.Contains("""<span class="row-line">Press Re-run thrice, intoning it passed locally, for the Machine Spirit rewards the…</span>""", html);
        Assert.DoesNotContain("depends on", html);
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
        var missing = new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), TimeProvider.System, NullLogger<RiteRepository>.Instance);
        var pages = new SitePages(missing, Options.Create(Settings.PublicPages), time, NullLogger<SitePages>.Instance);

        var today = await Read(context => pages.Today(context.Request, "", CancellationToken.None));
        var rite = await Read(context => pages.Rite(context.Request, "1", CancellationToken.None), "/r/1", "");
        var archive = await Read(context => pages.Archive(context.Request, CancellationToken.None), "/archive", "?before=4");

        var tag = await Read(context => pages.Tag(context.Request, "caches", CancellationToken.None), "/tags/caches", "");

        var search = await Read(context => pages.Archive(context.Request, CancellationToken.None), "/archive", "?q=cache");

        Assert.All([today, rite, archive, tag, search], page => Assert.Equal(StatusCodes.Status503ServiceUnavailable, page.Status));
        Assert.Contains("The rite has failed.", today.Html);
        Assert.Contains("""href="/archive?before=4">""", archive.Html);
        Assert.Equal(("no-store", ""), (archive.CacheControl, archive.AtCloudflare));
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
    // minute. While the app fails, Cloudflare may still show any of them for a week.
    [Fact]
    public async Task TodayAndARitesPage_StayAtTheEdgeUntilPurged_TheArchiveTagsAndQueriesBriefly()
    {
        var slumbering = await Read(context => Pages().Today(context.Request, "", CancellationToken.None));
        await Add(Day, "Litany of the Clean Cache");

        var today = await Read(context => Pages().Today(context.Request, "", CancellationToken.None));
        var rite = await Read(context => Pages().Rite(context.Request, "1", CancellationToken.None), "/r/1", "");
        var archive = await Read(context => Pages().Archive(context.Request, CancellationToken.None), "/archive", "");
        var withQuery = await Read(context => Pages().Today(context.Request, "", CancellationToken.None), "/", "?utm_source=feed");
        var tag = await Read(context => Pages().Tag(context.Request, "caches", CancellationToken.None), "/tags/caches", "");

        Assert.All([slumbering, today, rite, archive, tag, withQuery], page => Assert.Equal("public, max-age=60", page.CacheControl));
        Assert.All([slumbering, today, rite], page => Assert.Equal("public, max-age=3600, stale-if-error=604800", page.AtCloudflare));
        Assert.All([archive, tag, withQuery], page => Assert.Equal("public, max-age=60, stale-if-error=604800", page.AtCloudflare));
    }

    // A search engine indexes each page once, at the address it names, whatever query string reached it (a tracking
    // parameter, a link from a chat). The Archive's and a subject's older pages list other rites, so they keep theirs.
    [Fact]
    public async Task EveryPublicPage_NamesItsOneAddress()
    {
        await AddRites(3);
        await Tag(Day, "caches");

        var pages = new[]
        {
            await Read(context => Pages().Today(context.Request, "", CancellationToken.None), "/", "?utm_source=feed"),
            await Read(context => Pages().Rite(context.Request, "2", CancellationToken.None), "/r/2", "?ref=chat"),
            await Read(context => Pages().Archive(context.Request, CancellationToken.None), "/archive", ""),
            await Read(context => Pages().Archive(context.Request, CancellationToken.None), "/archive", "?before=2"),
            await Read(context => Pages().Tag(context.Request, "caches", CancellationToken.None), "/tags/caches", "?utm_source=feed"),
            await Read(context => Pages().Tag(context.Request, "caches", CancellationToken.None), "/tags/caches", "?before=3"),
            await Read(context => Pages().Reliquary(context.Request, CancellationToken.None), "/reliquary", ""),
            await Read(context => Task.FromResult<IActionResult>(Pages().About(context.Request)), "/catechism", "?from=nav"),
        };

        Assert.Equal(
            [
                $"https://{Site}/", $"https://{Site}/r/2", $"https://{Site}/archive", $"https://{Site}/archive?before=2",
                $"https://{Site}/tags/caches", $"https://{Site}/tags/caches?before=3", $"https://{Site}/reliquary", $"https://{Site}/catechism",
            ],
            pages.Select(page => Canonical(page.Html)));
        Assert.All(pages, page => Assert.DoesNotContain("noindex", page.Html));
    }

    // What a reader has witnessed (#198) only their browser knows: the Archive is the same for everyone, its count hidden
    // for the script to fill (in the margin, and on the sheet for a narrow screen), and every page says where that memory
    // is kept.
    [Fact]
    public async Task Archive_LeavesTheWitnessedCountToTheReadersBrowser()
    {
        var (_, empty) = await Archive("");
        await AddRites(3);

        var (_, archive) = await Archive("");
        var (_, today) = await Today("");

        Assert.Contains("""<div class="margin-witnessed" data-witnessed-note hidden>""", archive);
        Assert.Contains("""<p class="witnessed-count"><span data-witnessed>0</span> of 3 rites</p>""", archive);
        Assert.Contains("""<div class="sheet-witnessed" data-witnessed-note hidden>""", archive);
        Assert.Contains("""Hide witnessed rites <span class="witnessed-tally">· <span data-witnessed>0</span> of 3</span></label>""", archive);
        // Forgetting asks first, and isn't offered until the script has counted something to forget.
        Assert.Contains("""<button type="button" class="forget-ask" data-forget-ask hidden>Forget them…</button>""", archive);
        Assert.DoesNotContain("data-witnessed-note hidden", empty);
        Assert.All([archive, today], html => Assert.Contains("The rites you have read are remembered in your browser, and with your account if you sign in.", html));
    }

    // Drawing a rite (#196) is in the nav on every page, and in the Archive's margin once there's a rite to draw.
    [Fact]
    public async Task EveryPage_OffersToDrawARite_AndTheArchiveToo_OnceThereAreRites()
    {
        var (_, emptyArchive) = await Archive("");
        await AddRites(3);

        var (_, today) = await Today("");
        var (_, archive) = await Archive("");

        Assert.All([today, archive, emptyArchive], html =>
            Assert.Contains("""<a class="nav-draw" href="/draw" aria-label="Draw a rite" title="Draw a rite">""", html));
        Assert.Contains("""<a class="margin-draw" href="/draw">Draw a rite</a>""", archive);
        Assert.DoesNotContain("<a class=\"margin-draw\"", emptyArchive);
    }

    // The rites read one after another (#195): a rite's page leads to the one before and the one after, as links and in
    // its head (which the arrow keys follow); the first has no previous, the newest no next. The newest is Today's, so the
    // step to it leads to Today, where the step back came from.
    [Fact]
    public async Task ARitesPage_LeadsToTheRitesBeforeAndAfter()
    {
        await AddRites(4);

        var (_, first) = await Rite("1");
        var (_, middle) = await Rite("2");
        var (_, beforeTodays) = await Rite("3");
        var (_, newest) = await Rite("4");

        Assert.Equal(["next /r/2 Rite 2"], Steps(first));
        Assert.Equal(["prev /r/1 Rite 1", "next /r/3 Rite 3"], Steps(middle));
        Assert.Equal(["prev /r/2 Rite 2", "next / Rite 4"], Steps(beforeTodays));
        Assert.Equal(["prev /r/3 Rite 3"], Steps(newest));
        Assert.Contains("""<link rel="prev" href="/r/1">""", middle);
        Assert.Contains("""<link rel="next" href="/r/3">""", middle);
        Assert.Contains("""<link rel="next" href="/">""", beforeTodays);
        Assert.DoesNotContain("""rel="next""", newest);
        Assert.Contains("""<span class="sr-only">Previous rite, </span><span class="rite-step-number"><span aria-hidden="true">← NO. </span>1</span>""", middle);
    }

    // Today lists the rites before it already, so it doesn't show the previous one again: only its head names it.
    [Fact]
    public async Task Today_NamesTheRiteBeforeInItsHead_WithoutShowingItTwice()
    {
        await AddRites(2);

        var (_, html) = await Today("");

        Assert.Contains("""<link rel="prev" href="/r/1">""", html);
        Assert.DoesNotContain("""rel="next""", html);
        Assert.Empty(Steps(html));
    }

    // The rites before and after are a nicety: when they can't be read, the rite is still served.
    [Fact]
    public async Task ARitesPage_WhenItsNeighboursCantBeRead_IsStillServed_WithoutThem()
    {
        await AddRites(2);
        var pages = new SitePages(new RitesWithoutNeighbours(Repository), Options.Create(Settings.PublicPages), time, NullLogger<SitePages>.Instance);

        var page = await Read(context => pages.Rite(context.Request, "2", CancellationToken.None), "/r/2", "");

        Assert.Equal(StatusCodes.Status200OK, page.Status);
        Assert.Empty(Steps(page.Html));
    }

    // A feed reader given any page's address finds the feed (its head says where), and a reader finds it at the foot.
    [Fact]
    public async Task EveryPage_LeadsToTheFeed()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, today) = await Today("");
        var (_, rite) = await Rite("1");
        var (_, notFound) = await Today("no-such-page");

        Assert.All([today, rite, notFound], html =>
        {
            Assert.Contains("""<link rel="alternate" type="application/atom+xml" title="The Daily Machine Spirit" href="/feed.xml">""", html);
            Assert.Contains("""<a class="feed-link" href="/feed.xml">""", html);
        });
    }

    // Whatever anyone searched for must never become a page in an index, nor a missing page.
    [Fact]
    public async Task SearchResultsAndMissingPages_AreKeptOutOfIndexes()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, searched) = await Searched("Re-Run", "");
        var (_, missing) = await Rite("99");
        var (_, notFound) = await Today("no-such-page");

        Assert.All([searched, missing, notFound], html =>
        {
            Assert.Contains("""<meta name="robots" content="noindex">""", html);
            Assert.DoesNotContain("""rel="canonical""", html);
        });
    }

    // What a search engine shows of a rite: its title, opening words, day and subjects. Never its truth, sealed on the
    // page. Whatever the model wrote can't end the script element that carries it.
    [Fact]
    public async Task Rite_DescribesItselfForSearchEngines_WithoutItsTruth()
    {
        await Add(Day, "The Rite of </script> Re-Run");
        await Tag(Day, "caches", "builds");

        var (_, html) = await Rite("1");

        Assert.Contains("""<meta property="og:type" content="article">""", html);
        var json = Regex.Match(html, """<script type="application/ld\+json">(.*?)</script>""").Groups[1].Value;
        var data = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(
            ("CreativeWork", "The Rite of </script> Re-Run", "Press Re-run thrice, intoning it passed locally, for the Machine Spirit rewards the persistent.",
                "2026-10-09", $"https://{Site}/r/1", "Caches, Builds"),
            (data.GetProperty("@type").GetString(), data.GetProperty("name").GetString(), data.GetProperty("description").GetString(),
                data.GetProperty("datePublished").GetString(), data.GetProperty("url").GetString(), data.GetProperty("keywords").GetString()));
        Assert.DoesNotContain("timing", json);
    }

    // Guessing the truth (#199): a rite with false truths offers three readings, hidden until the script shows them
    // (only it can check the answer), its truth where data-truth says, and not always first; a rite without them keeps
    // the plain seal.
    [Fact]
    public async Task ARitesFalseTruths_AreOfferedWithItsTruth_InAnOrderOfItsOwn()
    {
        await AddRites(9);
        for (var number = 1; number <= 8; number++)
        {
            await Mislead(Day.AddDays(number - 9), "A false `one`.", "Another false one.");
        }

        var places = new List<int>();
        for (var number = 1; number <= 8; number++)
        {
            var (_, html) = await Rite(number.ToString(CultureInfo.InvariantCulture));
            var truthAt = int.Parse(Regex.Match(html, """<div class="guess" data-guess data-truth="(\d)" hidden>""").Groups[1].Value, CultureInfo.InvariantCulture);
            var readings = Regex.Matches(html, """<span class="reading-text">(.*?)</span>""").Select(match => match.Groups[1].Value).ToList();
            Assert.Equal(3, readings.Count);
            Assert.Equal("Your test depends on <code>timing</code>.", readings[truthAt - 1]);
            Assert.Equal(["A false <code>one</code>.", "Another false one."], readings.Where((_, at) => at != truthAt - 1));
            places.Add(truthAt);
        }
        var (_, plain) = await Rite("9");

        Assert.True(places.Distinct().Count() > 1, $"The truth stood at {string.Join(", ", places)}.");
        Assert.DoesNotContain(""""<div class="guess"""", plain);
        Assert.Contains("""<section class="truth" id="truth-panel" aria-labelledby="truth-heading" tabindex="-1" hidden>""", plain);
    }

    // A rite's link, pasted into a chat, shows the rite's own card (#191), its alt text the joke; any other page shows
    // the site's card; a page in no index names no picture.
    [Fact]
    public async Task LinkPreviews_ShowTheRitesOwnCard_OrTheSites()
    {
        await Add(Day, "The Rite of Re-Run");

        var (_, rite) = await Rite("1");
        var (_, today) = await Today("");
        var (_, missing) = await Rite("9");

        Assert.Contains($"""<meta property="og:image" content="https://{Site}/r/1/card.png">""", rite);
        Assert.Contains("""<meta property="og:image:alt" content="The Rite of Re-Run. Press Re-run thrice, intoning it passed locally, for the Machine Spirit rewards the persistent.">""", rite);
        Assert.Contains($"""<meta property="og:image" content="https://{Site}/card.png">""", today);
        Assert.All([rite, today], html =>
        {
            Assert.Contains("""<meta property="og:image:width" content="1200">""", html);
            Assert.Contains("""<meta property="og:image:height" content="630">""", html);
            Assert.Contains("""<meta name="twitter:card" content="summary_large_image">""", html);
        });
        Assert.DoesNotContain("og:image", missing);
    }

    // Blessings less heresies rank the rites; a tie goes to the more blessed. A rite as often called heresy as blessed,
    // or never voted on, isn't venerated. Kept at the edge until a vote purges it.
    [Fact]
    public async Task Reliquary_KeepsTheMostVeneratedRites_TheMostFirst()
    {
        await Add(Day.AddDays(-4), "The Unvoted Vigil");
        await Add(Day.AddDays(-3), "The Disputed Litany");
        await Add(Day.AddDays(-2), "The Twice Blessed Rite");
        await Add(Day.AddDays(-1), "The Contested Prayer");
        await Add(Day, "The Once Blessed Rite");
        var votes = new VoteRepository(cosmos.Container);
        foreach (var (number, voter, vote) in new[]
        {
            (2, "a", Reaction.Blessed), (2, "b", Reaction.Heresy),
            (3, "a", Reaction.Blessed), (3, "b", Reaction.Blessed),
            (4, "a", Reaction.Blessed), (4, "b", Reaction.Blessed), (4, "c", Reaction.Blessed), (4, "d", Reaction.Heresy),
            (5, "a", Reaction.Blessed),
        })
        {
            Ok(await votes.Vote(number, new VoterKey(voter), Some(vote), CancellationToken.None));
        }

        var page = await Read(context => Pages().Reliquary(context.Request, CancellationToken.None), "/reliquary", "");

        Assert.Equal(StatusCodes.Status200OK, page.Status);
        Assert.Equal(["The Contested Prayer", "The Twice Blessed Rite", "The Once Blessed Rite"], Titles(page.Html));
        Assert.Contains("<title>The Reliquary · The Daily Machine Spirit</title>", page.Html);
        Assert.Equal("public, max-age=3600, stale-if-error=604800", page.AtCloudflare);
        // The order says each one's place, so the rows wear no badge.
        Assert.DoesNotContain("""class="relic""", page.Html);
    }

    // The Archive in its other order: the Archive's link is current, and the order's own link says it's this one.
    [Fact]
    public async Task Reliquary_IsTheArchiveByVeneration()
    {
        await Add(Day, "The Unvoted Vigil");

        var page = await Read(context => Pages().Reliquary(context.Request, CancellationToken.None), "/reliquary", "");

        Assert.Contains("""<a href="/archive" aria-current="page">Archive</a>""", page.Html);
        Assert.Contains("""<a class="order" href="/archive">Newest</a>""", page.Html);
        Assert.Contains("""<a class="order" href="/reliquary" aria-current="page">Most venerated</a>""", page.Html);
    }

    [Fact]
    public async Task Reliquary_WithoutAVeneratedRite_SaysSo()
    {
        await Add(Day, "The Unvoted Vigil");

        var page = await Read(context => Pages().Reliquary(context.Request, CancellationToken.None), "/reliquary", "");

        Assert.Empty(Titles(page.Html));
        Assert.Contains("No rite is venerated yet.", page.Html);
    }

    // A vote's purge names /reliquary alone, so another spelling would keep old counts at Cloudflare for its hour.
    [Fact]
    public async Task Reliquary_SpelledAnotherWay_IsNotFound()
    {
        await Add(Day, "The Unvoted Vigil");

        var page = await Read(context => Pages().Reliquary(context.Request, CancellationToken.None), "/Reliquary", "");

        Assert.Equal((StatusCodes.Status404NotFound, "no-store"), (page.Status, page.CacheControl));
    }

    [Fact]
    public async Task Reliquary_WithATrailingSlash_RedirectsToItsAddress_KeepingTheQuery()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/reliquary/";
        context.Request.QueryString = new QueryString("?utm_source=feed");

        var result = Assert.IsType<RedirectResult>(await Pages().Reliquary(context.Request, CancellationToken.None));

        Assert.Equal(("/reliquary?utm_source=feed", true), (result.Url, result.Permanent));
    }

    [Fact]
    public async Task Archive_OffersItsOtherOrder_TheReliquary()
    {
        await Add(Day, "The Unvoted Vigil");

        var (_, html) = await Archive("");

        Assert.Contains("""<a class="order" href="/archive" aria-current="page">Newest</a>""", html);
        Assert.Contains("""<a class="order" href="/reliquary">Most venerated</a>""", html);
    }

    // Readers find the Reliquary through the rites in it: each wears its place, linking there.
    [Fact]
    public async Task Archive_MarksTheRitesTheReliquaryKeeps_WithTheirPlaces()
    {
        await Add(Day.AddDays(-3), "The Twice Blessed Rite");
        await Add(Day.AddDays(-2), "The Once Blessed Rite");
        await Add(Day.AddDays(-1), "The Unvoted Vigil");
        await Add(Day, "The Rite of Re-Run");
        var votes = new VoteRepository(cosmos.Container);
        Ok(await votes.Vote(1, new VoterKey("a"), Some(Reaction.Blessed), CancellationToken.None));
        Ok(await votes.Vote(1, new VoterKey("b"), Some(Reaction.Blessed), CancellationToken.None));
        Ok(await votes.Vote(2, new VoterKey("a"), Some(Reaction.Blessed), CancellationToken.None));

        var (_, html) = await Archive("");

        Assert.Equal(["2nd most venerated", "Most venerated"], Relics(html));
    }

    [Fact]
    public async Task RiteCard_OfARiteTheReliquaryKeeps_WearsItsPlace_OnTodayAndItsOwnPage()
    {
        await Add(Day, "The Once Blessed Rite");
        Ok(await new VoteRepository(cosmos.Container).Vote(1, new VoterKey("a"), Some(Reaction.Blessed), CancellationToken.None));

        var (_, today) = await Today("");
        var own = await Read(context => Pages().Rite(context.Request, "1", CancellationToken.None), "/r/1", "");

        Assert.All([today, own.Html], html =>
        {
            Assert.Equal(["Most venerated"], Relics(html));
            Assert.Contains("""<p class="margin-label" aria-hidden="true">In the Reliquary</p>""", html);
        });
    }

    // The connector's address and the skill's commands, each shown with a button that copies it as it is. Only a
    // deployment changes the page, and nothing purges it then, so it's kept briefly.
    [Fact]
    public async Task About_SaysHowToConsultTheMachineSpirit()
    {
        var about = await Read(context => Task.FromResult(Pages().About(context.Request)), "/catechism", "");

        Assert.Equal(StatusCodes.Status200OK, about.Status);
        Assert.Contains("<title>The Catechism · The Daily Machine Spirit</title>", about.Html);
        Assert.Contains("""<a href="/catechism" aria-current="page">About</a>""", about.Html);
        // The site's name heads it as it heads the Archive, so the small brand link would say it twice.
        Assert.Contains("""<p class="site-name">The Daily Machine Spirit</p>""", about.Html);
        Assert.DoesNotContain("class=\"brand\"", about.Html);
        Assert.Contains("<h2>Can my familiar consult the Machine Spirit?</h2>", about.Html);
        // Today's date, as the far future reckons it, is the example of how one is read.
        Assert.Contains("""<time datetime="2026-10-09">0 769 026.M3</time>""", about.Html);
        Assert.Contains("""<pre><code>https://mcp.dailymachinespirit.fyi/mcp</code></pre>""", about.Html);
        Assert.Contains("""data-copy="value" data-text="https://mcp.dailymachinespirit.fyi/mcp">""", about.Html);
        // The commands scroll rather than wrap, so a keyboard must be able to reach their block; their prompts are
        // shown, never copied.
        Assert.Contains("""<ul role="list">""", about.Html);
        Assert.Contains("""Every rite you vote on is kept in <a href="/breviary">your Breviary</a>.""", about.Html);
        Assert.Contains("""<pre tabindex="0" aria-label="Commands"><code><span class="prompt" aria-hidden="true">$ </span>git clone --depth 1 https://github.com/mykolad/daily-machine-spirit.git""", about.Html);
        Assert.Contains("""
            data-text="git clone --depth 1 https://github.com/mykolad/daily-machine-spirit.git
            mkdir -p ~/.claude/skills
            cp -r daily-machine-spirit/skills/consult-the-machine-spirit ~/.claude/skills/">
            """.ReplaceLineEndings("\n"), about.Html);
        Assert.Equal("public, max-age=60, stale-if-error=604800", about.AtCloudflare);
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

    private SitePages Pages() => new(Repository, Options.Create(Settings.PublicPages), time, NullLogger<SitePages>.Instance);

    private RiteRepository Repository => new(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance);

    private async Task<(int Status, string Html)> Today(string path)
    {
        var page = await Read(context => Pages().Today(context.Request, path, CancellationToken.None));
        return (page.Status, page.Html);
    }

    private async Task<(int Status, string Html)> Rite(string id)
    {
        var page = await Read(context => Pages().Rite(context.Request, id, CancellationToken.None), $"/r/{id}", "");
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

    private async Task<(int Status, string Html)> SearchedWithin(string words, string tag)
    {
        var query = QueryString.Create([new("q", words), new KeyValuePair<string, string?>("tag", tag)]);
        var page = await Read(context => Pages().Archive(context.Request, CancellationToken.None), "/archive", query.Value ?? "");
        return (page.Status, page.Html);
    }

    // Three rites mentioning a cache: one filed under Caches and Builds, one under Builds, one under nothing.
    private async Task AddCaches()
    {
        await Add(Day.AddDays(-2), "The Warm Cache");
        await Tag(Day.AddDays(-2), "caches", "builds");
        await Add(Day.AddDays(-1), "The Build Cache");
        await Tag(Day.AddDays(-1), "builds");
        await Add(Day, "The Cold Cache");
    }

    private static Task<(int Status, string Html, string CacheControl, string AtCloudflare)> Read(Func<HttpContext, Task<IActionResult>> serve)
        => Read(serve, "/", "");

    private static async Task<(int Status, string Html, string CacheControl, string AtCloudflare)> Read(
        Func<HttpContext, Task<IActionResult>> serve, string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(Site);
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        var result = Assert.IsType<ContentResult>(await serve(context));
        return (result.StatusCode ?? 0, result.Content ?? "", context.Response.Headers.CacheControl.ToString(),
            context.Response.Headers["Cloudflare-CDN-Cache-Control"].ToString());
    }

    // The links to the rites before and after, in order: which way, the number and the title.
    private static List<string> Steps(string html)
        => Regex.Matches(html, """<a class="rite-step[^"]*" href="([^"]+)" rel="(prev|next)">.*?<span class="rite-step-title">(.*?)</span></a>""")
            .Select(match => $"{match.Groups[2].Value} {match.Groups[1].Value} {match.Groups[3].Value}")
            .ToList();

    // The page's canonical address, which its link previews name too.
    private static string Canonical(string html)
    {
        var url = Regex.Match(html, """<link rel="canonical" href="([^"]+)">""").Groups[1].Value;
        Assert.Contains($"""<meta property="og:url" content="{url}">""", html);
        return url;
    }

    // The small parchments (Today's recent rites, More rites): each one's address, kicker and title, in order.
    private static List<string> Parchments(string html)
        => Regex.Matches(html, """<a class="parchment-card" href="(/r/\d+)">\s*<span class="parchment-card-kicker">(.+?)</span>\s*<span class="parchment-card-title">([^<]+)</span>""")
            .Select(card => $"{card.Groups[1].Value} {card.Groups[2].Value} {card.Groups[3].Value}")
            .ToList();

    // The small parchments' titles, in order: More rites on a rite's page.
    private static List<string> MoreTitles(string html)
        => html.Split("""<span class="parchment-card-title">""").Skip(1).Select(part => part[..part.IndexOf("</span>", StringComparison.Ordinal)]).ToList();

    // The archive rows' titles, in order, without a search's marks.
    private static List<string> Titles(string html)
        => Regex.Matches(html, """<a class="row-title" href="/r/\d+" title="[^"]+">(.*?)</a>""")
            .Select(match => match.Groups[1].Value.Replace("<mark>", string.Empty).Replace("</mark>", string.Empty))
            .ToList();

    // What each relic badge on the page says, in order, each leading to the Reliquary.
    private static List<string> Relics(string html)
        => Regex.Matches(html, """<a class="relic" href="/reliquary"><svg.*?</svg>(.*?)<span class="sr-only">, in the Reliquary</span></a>""")
            .Select(match => match.Groups[1].Value)
            .ToList();

    // When each countdown on the page says the next rite is due, in order.
    private static List<string> Countdowns(string html)
        => Regex.Matches(html, """data-countdown="([^"]+)">""").Select(match => match.Groups[1].Value).ToList();

    // "Rite 1" to "Rite <count>", a day apart, the last on Day: Today's.
    private async Task AddRites(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            await Add(Day.AddDays(i - count), $"Rite {i}");
        }
    }

    // As a rite written with false truths (#199), or given them since.
    private async Task Mislead(DateOnly day, string first, string second)
        => await cosmos.Container.PatchItemAsync<RiteDocument>(RiteDocument.IdFor(day), CosmosCalls.SharedPartitionKey,
            [Microsoft.Azure.Cosmos.PatchOperation.Set("/falseTruths", new FalseTruths(first, second))]);

    // As a rite whose writer wrote it the trouble it's for (#115).
    private async Task Ail(DateOnly day, string ailment)
        => await cosmos.Container.PatchItemAsync<RiteDocument>(RiteDocument.IdFor(day), CosmosCalls.SharedPartitionKey,
            [Microsoft.Azure.Cosmos.PatchOperation.Set("/ailment", ailment)]);

    // As a rite judged with these tags has them: only judging writes a rite's tags.
    private async Task Tag(DateOnly day, params string[] tags)
        => await cosmos.Container.PatchItemAsync<RiteDocument>(RiteDocument.IdFor(day), CosmosCalls.SharedPartitionKey,
            [Microsoft.Azure.Cosmos.PatchOperation.Set("/tags", tags)]);

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
