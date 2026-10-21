using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Api;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The public rites API (#101), against the Cosmos DB emulator: its shape is a contract with outside clients.</summary>
public sealed class RitesFunctionTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    // Every published rite, newest first: its words as stored, its subjects by key and label, and its page's address on
    // the site asked, its Heretical Truth sealed. Kept at the edge until a new rite or an erasure purges it, and readable
    // from any site's page.
    [Fact]
    public async Task Rites_ListsEveryRite_NewestFirst_WithItsSubjectsAndPage_ItsTruthSealed()
    {
        await Add(new DateOnly(2026, 10, 8), RiteKind.Prayer, "The Litany of the Warm Cache", Some(new[] { "caches", "builds" }));
        await Add(new DateOnly(2026, 10, 9), RiteKind.Ritual, "The Rite of Re-Run", None);
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("dailymachinespirit.fyi");

        var result = Assert.IsType<ContentResult>(await Function(Repository).Rites(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("application/json; charset=utf-8", result.ContentType);
        Assert.Equal("public, max-age=60, s-maxage=3600", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("*", context.Response.Headers.AccessControlAllowOrigin.ToString());
        // Unescaped, so a client searching the saved answer finds `it passed locally` as written.
        Assert.Contains("intoning `it passed locally`.", result.Content);
        var json = JsonSerializer.Deserialize<JsonElement>(result.Content ?? "{}");
        var rites = json.GetProperty("rites").EnumerateArray().ToList();
        Assert.Equal([2, 1], rites.Select(rite => rite.GetProperty("number").GetInt32()));
        var warm = rites[1];
        Assert.Equal(
            ("2026-10-08", "prayer", "The Litany of the Warm Cache", "Press Re-run thrice, intoning `it passed locally`.", "",
                "https://dailymachinespirit.fyi/r/1"),
            (warm.GetProperty("day").GetString(), warm.GetProperty("kind").GetString(), warm.GetProperty("title").GetString(),
                warm.GetProperty("text").GetString(), warm.GetProperty("ailment").GetString(), warm.GetProperty("url").GetString()));
        Assert.False(warm.TryGetProperty("hereticalTruth", out _));
        Assert.DoesNotContain("timing", result.Content);
        Assert.Equal(
            [("caches", "Caches"), ("builds", "Builds")],
            warm.GetProperty("subjects").EnumerateArray().Select(subject => (subject.GetProperty("key").GetString(), subject.GetProperty("label").GetString())));
        // A rite judged before tags existed is filed under nothing, rather than missing the field.
        Assert.Empty(rites[0].GetProperty("subjects").EnumerateArray());
    }

    // Any query string would be its own cache entry, and its own read of every rite: one address answers for all.
    [Fact]
    public async Task Rites_WithAQueryString_RedirectsToTheOneAddress()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?q=flaky");

        var result = Assert.IsType<RedirectResult>(await Function(Repository).Rites(context.Request, CancellationToken.None));

        Assert.Equal(("/api/v1/rites", true), (result.Url, result.Permanent));
        // A browser on another site follows it only if it may read it.
        Assert.Equal("*", context.Response.Headers.AccessControlAllowOrigin.ToString());
    }

    [Fact]
    public async Task Rites_WhenTheyCantBeRead_AreUnavailable_AndNotCached()
    {
        var missing = new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), NullLogger<RiteRepository>.Instance);
        var context = new DefaultHttpContext();

        var result = Assert.IsType<StatusCodeResult>(await Function(missing).Rites(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    // One rite's truth, only when asked for it, kept at the edge like the list.
    [Fact]
    public async Task HereticalTruth_RevealsThatRitesTruth()
    {
        await Add(new DateOnly(2026, 10, 8), RiteKind.Prayer, "The Litany of the Warm Cache", None);
        var context = new DefaultHttpContext();

        var result = Assert.IsType<ContentResult>(await Function(Repository).HereticalTruth(context.Request, 1, CancellationToken.None));

        Assert.Equal((StatusCodes.Status200OK, "application/json; charset=utf-8"), (result.StatusCode, result.ContentType));
        Assert.Equal("public, max-age=60, s-maxage=3600", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("*", context.Response.Headers.AccessControlAllowOrigin.ToString());
        Assert.Equal("""{"number":1,"hereticalTruth":"Your test depends on `timing`."}""", result.Content);
    }

    // Not kept, so the rite published with that number later is found.
    [Fact]
    public async Task HereticalTruth_OfNoRite_IsNotFound_AndNotCached()
    {
        var context = new DefaultHttpContext();

        var result = Assert.IsType<StatusCodeResult>(await Function(Repository).HereticalTruth(context.Request, 7, CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task HereticalTruth_WithAQueryString_RedirectsToItsOneAddress()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?reveal=yes");

        var result = Assert.IsType<RedirectResult>(await Function(Repository).HereticalTruth(context.Request, 3, CancellationToken.None));

        Assert.Equal(("/api/v1/rites/3/heretical-truth", true), (result.Url, result.Permanent));
        Assert.Equal("*", context.Response.Headers.AccessControlAllowOrigin.ToString());
    }

    private RiteRepository Repository => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private static RitesFunction Function(IRiteRepository rites) => new(rites, NullLogger<RitesFunction>.Instance);

    private async Task Add(DateOnly day, RiteKind kind, string title, Option<string[]> tags)
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
            Tags = tags,
        }, CancellationToken.None));
}
