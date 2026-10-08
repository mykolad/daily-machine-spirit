using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;

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
        Assert.DoesNotContain("Litany of the Clean Cache", html);
        Assert.DoesNotContain("Copy text", html);
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
    public async Task Rite_WithoutScores_OffersTheNewestOtherRites()
    {
        for (var daysAgo = 4; daysAgo >= 0; daysAgo--)
            await Add(Day.AddDays(-daysAgo), $"Rite {5 - daysAgo}");

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
        for (var daysAgo = 8; daysAgo >= 0; daysAgo--)
            await Add(Day.AddDays(-daysAgo), $"Rite {9 - daysAgo}");

        var (status, first) = await Archive("");
        var (_, second) = await Archive("3");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.DoesNotContain(">Rite 9<", first);
        Assert.Equal(["Rite 8", "Rite 7", "Rite 6", "Rite 5", "Rite 4", "Rite 3"], Titles(first));
        Assert.Contains("""href="/archive?before=3">Load older rites""", first);
        Assert.Equal(["Rite 2", "Rite 1"], Titles(second));
        Assert.Contains("The archive ends here.", second);
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
    public async Task EveryPage_WhenTheDataCantBeRead_FailsWith503_AndOffersToTryAgain()
    {
        var pages = new SitePages(
            new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), NullLogger<RiteRepository>.Instance),
            time,
            NullLogger<SitePages>.Instance);

        var today = await Read(context => pages.Today(context.Request, "", CancellationToken.None));
        var rite = await Read(context => pages.Rite(context.Request, "1", CancellationToken.None));
        var archive = await Read(context => pages.Archive(context.Request, CancellationToken.None), "/archive", "?before=4");

        Assert.All([today, rite, archive], page => Assert.Equal(StatusCodes.Status503ServiceUnavailable, page.Status));
        Assert.Contains("The rite has failed.", today.Html);
        Assert.Contains("""href="/archive?before=4">""", archive.Html);
        Assert.Equal("no-store", archive.CacheControl);
    }

    [Fact]
    public async Task Pages_AreCachedBriefly_AndEncodeWhatTheModelWrote()
    {
        await Add(Day, "<b>Bold</b> & `code`");

        var page = await Read(context => Pages().Today(context.Request, "", CancellationToken.None));

        Assert.Equal("public, max-age=60", page.CacheControl);
        Assert.Contains("&lt;b&gt;Bold&lt;/b&gt; &amp; `code`", page.Html);
        Assert.DoesNotContain("<b>Bold</b>", page.Html);
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

    // The archive rows' titles, in order.
    private static List<string> Titles(string html)
        => html.Split("""<span class="row-title">""").Skip(1).Select(part => part[..part.IndexOf("</span>", StringComparison.Ordinal)]).ToList();

    private async Task Add(DateOnly day, string title)
        => Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = day,
            Kind = RiteKind.Ritual,
            Title = title,
            Text = "Press Re-run thrice, intoning `it passed locally`, for the Machine Spirit rewards the persistent.",
            HereticalTruth = "Your test depends on `timing`.",
            GeneratedByModel = "gpt-6-sol",
            GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        }, CancellationToken.None));
}
