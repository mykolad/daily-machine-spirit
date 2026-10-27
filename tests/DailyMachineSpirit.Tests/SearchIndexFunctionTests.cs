using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public sealed class SearchIndexFunctionTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    // What the search field matches, newest first: the words as stored (as the Archive's search reads them) and the
    // subjects a word may name. Kept at the edge until a new rite purges it.
    [Fact]
    public async Task Index_ListsEveryRitesWordsAndSubjects_NewestFirst()
    {
        await Add(new DateOnly(2026, 10, 8), RiteKind.Prayer, "The Litany of the Warm Cache");
        await Add(new DateOnly(2026, 10, 9), RiteKind.Ritual, "The Rite of Re-Run");
        var context = new DefaultHttpContext();

        var result = Assert.IsType<ContentResult>(await Function(Repository).Index(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("application/json; charset=utf-8", result.ContentType);
        Assert.Equal(("public, max-age=60", "public, max-age=3600, stale-if-error=604800"),
            (context.Response.Headers.CacheControl.ToString(), context.Response.Headers["Cloudflare-CDN-Cache-Control"].ToString()));
        var entries = JsonSerializer.Deserialize<SearchIndexEntry[]>(result.Content ?? "[]", new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        Assert.Equal(
            [
                "2 2026-10-09 ritual The Rite of Re-Run | Press Re-run thrice, intoning `it passed locally`. | Your test depends on `timing`. | caches",
                "1 2026-10-08 prayer The Litany of the Warm Cache | Press Re-run thrice, intoning `it passed locally`. | Your test depends on `timing`. | caches",
            ],
            entries.Select(entry => $"{entry.Number} {entry.Day} {entry.Kind} {entry.Title} | {entry.Text} | {entry.Truth} | {string.Join(' ', entry.Tags)}"));
    }

    // Any query string would be its own cache entry, and its own read of every rite: one address answers for all.
    [Fact]
    public async Task Index_WithAQueryString_RedirectsToTheOneAddress()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?nonce=1");

        var result = Assert.IsType<RedirectResult>(await Function(Repository).Index(context.Request, CancellationToken.None));

        Assert.Equal(("/api/search-index", true), (result.Url, result.Permanent));
    }

    [Fact]
    public async Task Index_WhenTheRitesCantBeRead_IsUnavailable_AndNotCached()
    {
        var missing = new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), NullLogger<RiteRepository>.Instance);
        var context = new DefaultHttpContext();

        var result = Assert.IsType<StatusCodeResult>(await Function(missing).Index(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    private RiteRepository Repository => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private static SearchIndexFunction Function(IRiteRepository rites) => new(rites, NullLogger<SearchIndexFunction>.Instance);

    private async Task Add(DateOnly day, RiteKind kind, string title)
        => Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting
            {
                Kind = kind,
                Title = title,
                Text = "Press Re-run thrice, intoning `it passed locally`.",
                HereticalTruth = "Your test depends on `timing`.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            },
            Tags = new[] { "caches" },
        }, CancellationToken.None));
}
