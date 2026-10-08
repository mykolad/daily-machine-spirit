using System.Text;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Api;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class ReactionsTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task React_CountsANewReaction()
    {
        await AddRite();

        var counts = await React(Some(Reaction.Blessed), None);

        Assert.Equal(new ReactionCounts(1, 0), counts);
    }

    [Fact]
    public async Task React_MovesAReaction_FromOneToTheOther()
    {
        await AddRite();
        await React(Some(Reaction.Blessed), None);

        var counts = await React(Some(Reaction.Heresy), Some(Reaction.Blessed));

        Assert.Equal(new ReactionCounts(0, 1), counts);
    }

    [Fact]
    public async Task React_TakesAReactionBack()
    {
        await AddRite();
        await React(Some(Reaction.Heresy), None);

        var counts = await React(None, Some(Reaction.Heresy));

        Assert.Equal(new ReactionCounts(0, 0), counts);
    }

    [Fact]
    public async Task React_TheSameAsBefore_ChangesNothing()
    {
        await AddRite();
        await React(Some(Reaction.Blessed), None);

        var counts = await React(Some(Reaction.Blessed), Some(Reaction.Blessed));

        Assert.Equal(new ReactionCounts(1, 0), counts);
    }

    [Fact]
    public async Task React_NeverTakesACountBelowZero_WhateverTheBrowserClaims()
    {
        await AddRite();

        var moved = await React(Some(Reaction.Blessed), Some(Reaction.Heresy));
        var takenBack = await React(None, Some(Reaction.Heresy));

        Assert.Equal(new ReactionCounts(1, 0), moved);
        Assert.Equal(new ReactionCounts(1, 0), takenBack);
    }

    [Fact]
    public async Task React_ToARiteThatDoesNotExist_IsNone()
        => Assert.True(Ok(await Repository.React(7, Some(Reaction.Blessed), None, CancellationToken.None)).IsNone);

    [Fact]
    public async Task ThePageCounts_IncludeTheReactions()
    {
        await AddRite();
        await React(Some(Reaction.Blessed), None);
        await React(Some(Reaction.Blessed), None);
        await React(Some(Reaction.Heresy), None);

        var rite = Ok(await Repository.GetByNumber(1, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("No rite."));

        Assert.Equal((2, 1), (rite.BlessedCount, rite.HeresyCount));
    }

    [Fact]
    public async Task Post_SavesTheReaction_AndAnswersTheCounts()
    {
        await AddRite();

        var (status, body) = await Post(1, """{"reaction":"blessed","previous":null}""", "application/json");

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("""{"blessed":1,"heresy":0}""", body);
    }

    [Theory]
    [InlineData("""{"reaction":"amen"}""")]
    [InlineData("""{"reaction":1}""")]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task Post_WithABodyThatIsNotAReaction_IsABadRequest(string json)
    {
        await AddRite();

        var (status, _) = await Post(1, json, "application/json");

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(new ReactionCounts(0, 0), await React(None, None));
    }

    [Fact]
    public async Task Post_WithoutJson_IsRefused_SoAnotherSitesFormCantReact()
    {
        await AddRite();

        var (status, _) = await Post(1, "reaction=blessed", "application/x-www-form-urlencoded");

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, status);
    }

    [Fact]
    public async Task Post_ToARiteThatDoesNotExist_IsNotFound()
    {
        var (status, _) = await Post(9, """{"reaction":"heresy"}""", "application/json");

        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task Post_WhenTheDataCantBeSaved_Is503()
    {
        var function = new ReactionsFunction(
            new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), NullLogger<RiteRepository>.Instance),
            NullLogger<ReactionsFunction>.Instance);

        var (status, _) = await Post(function, 1, """{"reaction":"heresy"}""", "application/json");

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    private RiteRepository Repository => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private async Task<ReactionCounts> React(Option<Reaction> reaction, Option<Reaction> previous)
        => Ok(await Repository.React(1, reaction, previous, CancellationToken.None))
            .IfNone(() => throw new Xunit.Sdk.XunitException("Expected rite NO. 1."));

    private Task<(int Status, string Body)> Post(int number, string body, string contentType)
        => Post(new ReactionsFunction(Repository, NullLogger<ReactionsFunction>.Instance), number, body, contentType);

    private static async Task<(int Status, string Body)> Post(ReactionsFunction function, int number, string body, string contentType)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var result = await function.React(context.Request, number, CancellationToken.None);
        return result switch
        {
            OkObjectResult ok => (StatusCodes.Status200OK, JsonSerializer.Serialize(ok.Value)),
            IStatusCodeActionResult other => (other.StatusCode ?? 0, ""),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected result {result}."),
        };
    }

    private async Task AddRite()
        => Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = new DateOnly(2026, 10, 9),
            Kind = RiteKind.Prayer,
            Title = "Litany of the Clean Cache",
            Text = "Clear the cache, and the cache shall clear thee.",
            HereticalTruth = "A stale cache hides a missing invalidation.",
            GeneratedByModel = "gpt-6-sol",
            GeneratedAtUtc = new DateTime(2026, 10, 9, 0, 0, 5, DateTimeKind.Utc),
        }, CancellationToken.None));
}
