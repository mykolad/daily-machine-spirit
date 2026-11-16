using System.Xml.Linq;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Feeds;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public sealed class FeedFunctionTests : IAsyncLifetime
{
    private const string Site = "dailymachinespirit.test";

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    private readonly CosmosTestContainer cosmos = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 9, 20, 48, 0, TimeSpan.Zero));

    public ValueTask InitializeAsync() => cosmos.InitializeAsync();

    public ValueTask DisposeAsync() => cosmos.DisposeAsync();

    // Each rite an entry, newest first: its title, page, day, kind and subjects, and its text, the truth left sealed.
    // Kept at the edge until a new rite or an erasure purges it.
    [Fact]
    public async Task Feed_ListsTheNewestRites_EachWithItsTextAndPage_ItsTruthSealed()
    {
        await Add(new DateOnly(2026, 10, 8), RiteKind.Prayer, "The Litany of the Warm Cache", "Bless the cache.");
        await Add(new DateOnly(2026, 10, 9), RiteKind.Ritual, "The Rite of Re-Run", "Press Re-run thrice.");
        await Tag(new DateOnly(2026, 10, 9), "builds", "testing");
        var context = Request("");

        var result = Assert.IsType<ContentResult>(await Function(Repository).Feed(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("application/atom+xml; charset=utf-8", result.ContentType);
        Assert.Equal(("public, max-age=60", "public, max-age=3600, stale-if-error=604800"),
            (context.Response.Headers.CacheControl.ToString(), context.Response.Headers["Cloudflare-CDN-Cache-Control"].ToString()));
        var feed = Parse(result);
        Assert.Equal(("The Daily Machine Spirit", "2026-10-09T00:00:00Z", $"https://{Site}/feed.xml"),
            (feed.Element(Atom + "title")?.Value, feed.Element(Atom + "updated")?.Value, Link(feed, "self")));
        var entries = feed.Elements(Atom + "entry").ToList();
        Assert.Equal(
            [
                $"The Rite of Re-Run | https://{Site}/r/2 | 2026-10-09T00:00:00Z | ritual builds testing",
                $"The Litany of the Warm Cache | https://{Site}/r/1 | 2026-10-08T00:00:00Z | prayer",
            ],
            entries.Select(entry => string.Join(" | ",
                entry.Element(Atom + "title")?.Value, Link(entry, "alternate"), entry.Element(Atom + "published")?.Value,
                string.Join(' ', entry.Elements(Atom + "category").Select(category => category.Attribute("term")?.Value)))));
        Assert.Equal(
            $"""<p>Press Re-run thrice.</p><p>The Heretical Truth is sealed. Break the seal: <a href="https://{Site}/r/2">https://{Site}/r/2</a></p>""",
            entries[0].Element(Atom + "content")?.Value);
        Assert.DoesNotContain("timing", result.Content);
    }

    // What a model wrote is the entry's text, never markup: escaped for the HTML a reader renders, then for the XML that
    // carries it. Code shows as code, an erasure's emoji stays, and a character XML can't carry is dropped.
    [Fact]
    public async Task Feed_EncodesWhatTheModelWrote()
    {
        await Add(new DateOnly(2026, 10, 9), RiteKind.Ritual, "Fish & <Chips>", "If a < b && `rm -rf` 💀\u0001 then pray.");
        var context = Request("");

        var result = Assert.IsType<ContentResult>(await Function(Repository).Feed(context.Request, CancellationToken.None));

        var entry = Assert.Single(Parse(result).Elements(Atom + "entry"));
        Assert.Equal("Fish & <Chips>", entry.Element(Atom + "title")?.Value);
        Assert.StartsWith("<p>If a &lt; b &amp;&amp; <code>rm -rf</code> 💀 then pray.</p>", entry.Element(Atom + "content")?.Value);
    }

    // A new environment's feed is valid, and empty.
    [Fact]
    public async Task Feed_WithoutRites_IsEmpty_AsOfNow()
    {
        var result = Assert.IsType<ContentResult>(await Function(Repository).Feed(Request("").Request, CancellationToken.None));

        var feed = Parse(result);
        Assert.Equal("2026-10-09T20:48:00Z", feed.Element(Atom + "updated")?.Value);
        Assert.Empty(feed.Elements(Atom + "entry"));
    }

    // Any query string would be its own cache entry, and its own read: one address answers for all.
    [Fact]
    public async Task Feed_WithAQueryString_RedirectsToTheOneAddress()
    {
        var result = Assert.IsType<RedirectResult>(await Function(Repository).Feed(Request("?utm_source=reader").Request, CancellationToken.None));

        Assert.Equal(("/feed.xml", true), (result.Url, result.Permanent));
    }

    [Fact]
    public async Task Feed_WhenTheRitesCantBeRead_IsUnavailable_AndNotCached()
    {
        var missing = new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), TimeProvider.System, NullLogger<RiteRepository>.Instance);
        var context = Request("");

        var result = Assert.IsType<StatusCodeResult>(await Function(missing).Feed(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    private RiteRepository Repository => new(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance);

    private FeedFunction Function(IRiteRepository rites) => new(rites, time, NullLogger<FeedFunction>.Instance);

    private static XElement Parse(ContentResult result) => XDocument.Parse(result.Content ?? "").Root ?? new XElement("none");

    private static string Link(XElement element, string rel)
        => element.Elements(Atom + "link").Where(link => link.Attribute("rel")?.Value == rel).Select(link => link.Attribute("href")?.Value ?? "").FirstOrDefault() ?? "";

    private static DefaultHttpContext Request(string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(Site);
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    // As a rite judged with these tags has them: only judging writes a rite's tags.
    private async Task Tag(DateOnly day, params string[] tags)
        => await cosmos.Container.PatchItemAsync<RiteDocument>(RiteDocument.IdFor(day), CosmosCalls.SharedPartitionKey,
            [Microsoft.Azure.Cosmos.PatchOperation.Set("/tags", tags)]);

    private async Task Add(DateOnly day, RiteKind kind, string title, string text)
        => Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting
            {
                Kind = kind,
                Title = title,
                Text = text,
                HereticalTruth = "Your test depends on `timing`.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            },
        }, CancellationToken.None));
}
