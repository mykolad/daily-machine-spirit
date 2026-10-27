using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Voters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class BreviaryTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();
    private readonly VoterSession session = new(
        Options.Create(new VoterOptions
        {
            KeySecret = "key-secret-of-at-least-32-characters",
            SessionSecret = "session-secret-of-at-least-32-characters",
        }),
        TimeProvider.System);

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task SignedOut_OffersTheSignIn_AndIsNeverCached()
    {
        var (status, html, headers) = await Get(Function(), string.Empty, string.Empty);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("""<a class="sheet-button" href="/auth/github?return=%2Fbreviary">Sign in with GitHub</a>""", html);
        NeverCached(headers);
    }

    [Fact]
    public async Task SignedIn_ListsTheRitesVotedOn_NewestFirst_EachWithItsVerdict_AndNoOneElses()
    {
        await AddRites(4);
        await Vote(1, Reaction.Blessed);
        await Vote(3, Reaction.Heresy);
        Ok(await Votes.Vote(2, "someone-else", Some(Reaction.Blessed), CancellationToken.None));

        var (status, html, headers) = await Get(Function(), string.Empty, SignedIn());

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(["/r/3", "/r/1"], Titles(html));
        Assert.Contains("You called it heresy</span>", html);
        Assert.Contains("You blessed it</span>", html);
        Assert.Contains("""<a class="verdict" href="/breviary" aria-current="page">All 2</a>""", html);
        Assert.Contains("2 rites you voted on, by subject.", html);
        // The nav's link, which the script shows to a signed-in reader, says it's this page.
        Assert.Contains("""<a class="nav-breviary" href="/breviary" aria-label="Your Breviary" title="Your Breviary" aria-current="page" hidden>""", html);
        NeverCached(headers);
    }

    [Theory]
    [InlineData("?verdict=blessed", "/r/1")]
    [InlineData("?verdict=heresy", "/r/3")]
    public async Task OneVerdict_ShowsOnlyItsRites(string query, string rite)
    {
        await AddRites(3);
        await Vote(1, Reaction.Blessed);
        await Vote(3, Reaction.Heresy);

        var (_, html, _) = await Get(Function(), query, SignedIn());

        Assert.Equal([rite], Titles(html));
        Assert.Contains($"""href="/breviary{query}" aria-current="page">""", html);
    }

    [Fact]
    public async Task WithoutAVote_SaysSo()
    {
        var (status, html, _) = await Get(Function(), string.Empty, SignedIn());

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Contains("You haven’t voted on a rite yet.", html);
    }

    [Fact]
    public async Task TheRites_AreInAChapterPerSubject_ByName_UnderTheirLikeliest_WithTheUnfiledLast()
    {
        await AddRite(1, "First", ["caches", "builds"]);
        await AddRite(2, "Second", ["builds"]);
        await AddRite(3, "Third", []);
        await AddRite(4, "Fourth", ["caches"]);
        foreach (var number in new[] { 1, 2, 3, 4 })
        {
            await Vote(number, Reaction.Blessed);
        }

        var (_, html, _) = await Get(Function(), string.Empty, SignedIn());

        Assert.Equal(["chapter-builds", "chapter-caches", "chapter-unfiled"], Chapters(html));
        Assert.Equal(["/r/2", "/r/4", "/r/1", "/r/3"], Titles(html));
        Assert.Contains("""<a href="#chapter-caches"><span>Caches</span><span class="chapter-count">2<span class="sr-only"> rites</span></span></a>""", html);
    }

    [Fact]
    public async Task Search_ShowsTheRitesHoldingEveryWord_AndTheVerdictsKeepTheWords()
    {
        await AddRite(1, "The Rite of the Empty Ledger", []);
        await AddRite(2, "The Ledger Unbalanced", []);
        await AddRite(3, "The Vestment of Any", []);
        await Vote(1, Reaction.Blessed);
        await Vote(2, Reaction.Heresy);
        await Vote(3, Reaction.Blessed);

        var (status, html, headers) = await Get(Function(), "?q=ledger", SignedIn());

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal(["/r/2", "/r/1"], Titles(html));
        Assert.Contains("<title>“ledger” in your Breviary: 2 rites", html);
        Assert.Contains("2 of your rites mention it.", html);
        // Told apart from the nav's search, which looks through every rite.
        Assert.Contains("""<form class="sheet-search" role="search" aria-label="Your Breviary" """, html);
        Assert.Contains("""<a class="verdict" href="/breviary?verdict=blessed&amp;q=ledger">Blessed 1</a>""", html);
        Assert.Contains("""<a class="sheet-clear" href="/breviary" aria-label="Clear search">""", html);
        NeverCached(headers);
    }

    [Fact]
    public async Task Search_WithinAVerdict_KeepsItInTheFormAndWhenCleared()
    {
        await AddRite(1, "The Rite of the Empty Ledger", []);
        await AddRite(2, "The Ledger Unbalanced", []);
        await Vote(1, Reaction.Blessed);
        await Vote(2, Reaction.Heresy);

        var (_, html, _) = await Get(Function(), "?verdict=heresy&q=ledger", SignedIn());

        Assert.Equal(["/r/2"], Titles(html));
        // What's shown is counted, not what the other verdict holds.
        Assert.Contains("<title>“ledger” in your Breviary: 1 rite ·", html);
        Assert.Contains("1 of your rites mentions it.", html);
        Assert.Contains("""<a class="verdict" href="/breviary?q=ledger">All 2</a>""", html);
        Assert.Contains("""<input type="hidden" name="verdict" value="heresy">""", html);
        Assert.Contains("""<a class="sheet-clear" href="/breviary?verdict=heresy" aria-label="Clear search">""", html);
    }

    [Fact]
    public async Task Search_FindsTheRitesFiledUnderASubjectAWordNames()
    {
        await AddRite(1, "The Muffled Alarm", ["builds"]);
        await AddRite(2, "The Warm Cache", ["caches"]);
        await Vote(1, Reaction.Blessed);
        await Vote(2, Reaction.Blessed);

        var (_, html, _) = await Get(Function(), "?q=build", SignedIn());

        Assert.Equal(["/r/1"], Titles(html));
    }

    [Fact]
    public async Task Search_FindingNothing_SaysSo_AndLeadsBackToTheWholeBreviary()
    {
        await AddRites(1);
        await Vote(1, Reaction.Blessed);

        var (_, html, _) = await Get(Function(), "?q=ledger", SignedIn());

        Assert.Empty(Titles(html));
        Assert.Contains("<title>“ledger” in your Breviary: no rites", html);
        Assert.Contains("""<a class="sheet-button" href="/breviary">Show your whole Breviary</a>""", html);
    }

    // The verdict counts the others who gave it, so no vote is said twice; the other verdict keeps its count.
    [Fact]
    public async Task AVerdict_CountsTheOthersWhoGaveIt_BesideTheOtherVerdictsCount()
    {
        await AddRites(2);
        await Vote(1, Reaction.Blessed);
        await Vote(2, Reaction.Heresy);
        foreach (var (voter, reaction) in new[] { ("a", Reaction.Blessed), ("b", Reaction.Blessed), ("c", Reaction.Heresy) })
        {
            Ok(await Votes.Vote(1, voter, Some(reaction), CancellationToken.None));
        }

        var (_, html, _) = await Get(Function(), string.Empty, SignedIn());

        Assert.Contains("You and 2 others blessed it</span>", html);
        Assert.Contains("Heresy 1</span>", html);
        Assert.DoesNotContain("Blessed 3", html);
        Assert.Contains("You called it heresy</span></span>", html);
    }

    [Fact]
    public async Task WhenTheVotesCantBeRead_Is503_AndNeverCached()
    {
        var missing = cosmos.Container.Database.GetContainer("no-such-container");
        var function = new BreviaryFunction(
            new VoteRepository(missing), new RiteRepository(missing, NullLogger<RiteRepository>.Instance), session,
            NullLogger<BreviaryFunction>.Instance);

        var (status, _, headers) = await Get(function, string.Empty, SignedIn());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        NeverCached(headers);
    }

    private VoteRepository Votes => new(cosmos.Container);

    private BreviaryFunction Function()
        => new(Votes, new RiteRepository(cosmos.Container, NullLogger<RiteRepository>.Instance), session, NullLogger<BreviaryFunction>.Instance);

    private string VoterKey => session.VoterKeyFor("github", "583231");

    private string SignedIn() => $"{VoterSession.CookieName}={session.Issue(VoterKey)}";

    private async Task Vote(int number, Reaction reaction) => Ok(await Votes.Vote(number, VoterKey, Some(reaction), CancellationToken.None));

    private static async Task<(int Status, string Html, IHeaderDictionary Headers)> Get(BreviaryFunction function, string query, string cookie)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "GET";
        request.Path = Paths.Breviary;
        request.QueryString = new QueryString(query);
        if (cookie.Length > 0)
        {
            request.Headers.Cookie = cookie;
        }
        var page = Assert.IsType<ContentResult>(await function.Breviary(request, CancellationToken.None));
        return (page.StatusCode ?? 0, page.Content ?? string.Empty, request.HttpContext.Response.Headers);
    }

    // The rows' links, in the order shown.
    private static List<string> Titles(string html)
        => Regex.Matches(html, "<a class=\"row-title\" href=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

    // The chapters' rubrics, in the order shown.
    private static List<string> Chapters(string html)
        => Regex.Matches(html, "<h2 class=\"chapter-rubric\" id=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

    private static void NeverCached(IHeaderDictionary headers)
    {
        Assert.Equal("private, no-store", headers.CacheControl.ToString());
        Assert.Equal("no-store", headers["Cloudflare-CDN-Cache-Control"].ToString());
    }

    private async Task AddRites(int count)
    {
        for (var number = 1; number <= count; number++)
        {
            await AddRite(number, $"Litany of the Clean Cache {number}", []);
        }
    }

    // Numbered as added, a day apart.
    private async Task AddRite(int number, string title, string[] tags)
    {
        var rites = new RiteRepository(cosmos.Container, NullLogger<RiteRepository>.Instance);
        Ok(await rites.Add(new Rite
        {
            PublishedOnUtc = new DateOnly(2026, 10, 8).AddDays(number),
            Writing = new RiteWriting
            {
                Kind = RiteKind.Prayer,
                Title = title,
                Text = "Clear the cache, and the cache shall clear thee.",
                HereticalTruth = "A stale cache hides a missing invalidation.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = new DateTime(2026, 10, 8, 23, 0, 0, DateTimeKind.Utc),
            },
            Tags = tags.Length == 0 ? None : Some(tags),
        }, CancellationToken.None));
    }
}
