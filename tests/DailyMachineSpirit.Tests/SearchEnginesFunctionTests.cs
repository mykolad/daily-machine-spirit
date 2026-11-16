using System.Xml.Linq;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.SearchEngines;
using DailyMachineSpirit.Functions.Tags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public sealed class SearchEnginesFunctionTests : IAsyncLifetime
{
    private const string Site = "dailymachinespirit.test";

    private static readonly XNamespace Urls = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private readonly CosmosTestContainer cosmos = new();

    public ValueTask InitializeAsync() => cosmos.InitializeAsync();

    public ValueTask DisposeAsync() => cosmos.DisposeAsync();

    // Every public page, each rite with its day, and Today and the Archive with the newest rite's. Kept at the edge until
    // a new rite purges it.
    [Fact]
    public async Task Sitemap_ListsEveryPublicPage_EachRiteWithItsDay()
    {
        await Add(new DateOnly(2026, 10, 8), "The Litany of the Warm Cache");
        await Add(new DateOnly(2026, 10, 9), "The Rite of Re-Run");
        var context = Request("/sitemap.xml", "");

        var result = Assert.IsType<ContentResult>(await Function(Repository).Sitemap(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("application/xml; charset=utf-8", result.ContentType);
        Assert.Equal(("public, max-age=60", "public, max-age=3600, stale-if-error=604800"),
            (context.Response.Headers.CacheControl.ToString(), context.Response.Headers["Cloudflare-CDN-Cache-Control"].ToString()));
        var urls = XDocument.Parse(result.Content ?? "").Root?.Elements(Urls + "url")
            .Select(url => $"{url.Element(Urls + "loc")?.Value} {url.Element(Urls + "lastmod")?.Value}".TrimEnd())
            .ToList() ?? [];
        Assert.Equal(
            [
                $"https://{Site}/ 2026-10-09", $"https://{Site}/archive 2026-10-09", $"https://{Site}/reliquary", $"https://{Site}/catechism",
                .. RiteTags.All.Select(tag => $"https://{Site}/tags/{tag.Key}"),
                $"https://{Site}/r/2 2026-10-09", $"https://{Site}/r/1 2026-10-08",
            ],
            urls);
    }

    // Cloudflare would keep any other spelling apart, where a new rite's purge never reaches: one address answers for all.
    [Theory]
    [InlineData("/sitemap.xml", "?page=2")]
    [InlineData("/Sitemap.xml", "")]
    [InlineData("/sitemap.xml/", "")]
    public async Task Sitemap_SpelledAnotherWay_RedirectsToTheOneAddress(string path, string query)
    {
        var context = Request(path, query);

        var result = Assert.IsType<RedirectResult>(await Function(Repository).Sitemap(context.Request, CancellationToken.None));

        Assert.Equal(("/sitemap.xml", true), (result.Url, result.Permanent));
    }

    [Fact]
    public async Task Sitemap_WhenTheRitesCantBeRead_IsUnavailable_AndNotCached()
    {
        var missing = new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), TimeProvider.System, NullLogger<RiteRepository>.Instance);
        var context = Request("/sitemap.xml", "");

        var result = Assert.IsType<StatusCodeResult>(await Function(missing).Sitemap(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    // Crawlers stay out of the Scriptorium, a reader's own pages, sign-in and the APIs, and learn where the sitemap is.
    [Fact]
    public void Robots_KeepsCrawlersToThePublicPages_AndNamesTheSitemap()
    {
        var context = Request("/robots.txt", "");

        var result = Assert.IsType<ContentResult>(Function(Repository).Robots(context.Request));

        Assert.Equal("text/plain; charset=utf-8", result.ContentType);
        Assert.Equal(
            ["User-agent: *", "Disallow: /scriptorium", "Disallow: /breviary", "Disallow: /auth/", "Disallow: /api/", "", $"Sitemap: https://{Site}/sitemap.xml"],
            (result.Content ?? "").TrimEnd().Split('\n').Select(line => line.TrimEnd('\r')));
    }

    private RiteRepository Repository => new(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance);

    private static SearchEnginesFunction Function(IRiteRepository rites) => new(rites, NullLogger<SearchEnginesFunction>.Instance);

    private static DefaultHttpContext Request(string path, string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(Site);
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    private async Task Add(DateOnly day, string title)
        => Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting
            {
                Kind = RiteKind.Ritual,
                Title = title,
                Text = "Press Re-run thrice, intoning `it passed locally`.",
                HereticalTruth = "Your test depends on `timing`.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            },
        }, CancellationToken.None));
}
