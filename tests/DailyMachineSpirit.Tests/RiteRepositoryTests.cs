using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class RiteRepositoryTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    private RiteRepository Repository => new(cosmos.Container);

    private static Rite MakeRite(DateOnly date, string title) => new()
    {
        PublishedOnUtc = date,
        Kind = RiteKind.Ritual,
        Title = title,
        Text = "Press Re-run thrice, intoning `it passed locally`.",
        HereticalTruth = "Your test depends on timing.",
        GeneratedByModel = "gpt-6-sol",
        GeneratedAtUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
    };

    private async Task<Rite> AddOrFail(Rite rite)
        => (await Repository.Add(rite, CancellationToken.None)).Match(
            Right: saved => saved,
            Left: error => throw new Xunit.Sdk.XunitException($"Expected the rite to be saved, but: {error.Message}"));

    private async Task<Rite> PublishedOnOrFail(DateOnly date)
        => (await Repository.GetPublishedOn(date, CancellationToken.None))
            .IfNone(() => throw new Xunit.Sdk.XunitException($"Expected a rite on {date}."));

    [Fact]
    public async Task Add_SavesTheRite_ThenFindsItByNumberAndDay()
    {
        var date = new DateOnly(2026, 10, 7);

        var saved = await AddOrFail(MakeRite(date, "The Rite of Re-Run"));

        var byNumber = await Repository.GetByNumber(saved.Number, CancellationToken.None);
        var byDay = await Repository.GetPublishedOn(date, CancellationToken.None);
        Assert.Equal(Some("The Rite of Re-Run"), byNumber.Map(rite => rite.Title));
        Assert.Equal(Some(RiteKind.Ritual), byNumber.Map(rite => rite.Kind));
        Assert.Equal(Some("Press Re-run thrice, intoning `it passed locally`."), byDay.Map(rite => rite.Text));
        Assert.Equal(Some(saved.Number), byDay.Map(rite => rite.Number));
    }

    [Fact]
    public async Task Add_NumbersRitesOneAfterAnother()
    {
        var first = await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "First"));
        var second = await AddOrFail(MakeRite(new DateOnly(2026, 10, 8), "Second"));

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
    }

    [Fact]
    public async Task Add_ForADayThatHasARite_SavesNothing_AndUsesNoNumber()
    {
        var date = new DateOnly(2026, 10, 7);
        await AddOrFail(MakeRite(date, "First"));

        var second = await Repository.Add(MakeRite(date, "Second"), CancellationToken.None);
        var next = await AddOrFail(MakeRite(date.AddDays(1), "Next day"));

        Assert.Equal(Left<Error, Rite>(RiteRepository.DayAlreadyHasRite), second);
        Assert.Equal("First", (await PublishedOnOrFail(date)).Title);
        Assert.Equal(2, next.Number);
    }

    [Fact]
    public async Task Add_ManyAtOnce_GivesEachDayItsOwnNumber()
    {
        var rites = Enumerable.Range(1, 4).Select(day => MakeRite(new DateOnly(2026, 10, day), $"Day {day}"));

        var saved = await Task.WhenAll(rites.Select(AddOrFail));

        Assert.Equal([1, 2, 3, 4], saved.Select(rite => rite.Number).Order());
    }

    [Fact]
    public async Task GetByNumber_AndGetPublishedOn_FindNothingWhenMissing()
    {
        Assert.Equal(Option<Rite>.None, await Repository.GetByNumber(42, CancellationToken.None));
        Assert.Equal(Option<Rite>.None, await Repository.GetPublishedOn(new DateOnly(2026, 1, 1), CancellationToken.None));
    }

    [Fact]
    public async Task GetPublishedOn_ADocumentWithANullTitle_FailsInsteadOfReturningANull()
    {
        var date = new DateOnly(2026, 10, 7);
        await cosmos.Container.CreateItemAsync(new
        {
            id = "2026-10-07", partition = "rites", type = "rite", number = 1, publishedOnUtc = date, kind = "prayer",
            title = (string?)null, text = "", hereticalTruth = "", generatedByModel = "", generatedAtUtc = DateTime.UtcNow,
        });

        await Assert.ThrowsAnyAsync<Exception>(() => Repository.GetPublishedOn(date, CancellationToken.None));
    }

    [Fact]
    public async Task GetNewest_ReturnsTheNewestFirst()
    {
        foreach (var day in new[] { 3, 5, 4 })
            await AddOrFail(MakeRite(new DateOnly(2026, 10, day), $"Day {day}"));

        var newest = await Repository.GetNewest(2, CancellationToken.None);

        Assert.Equal(["Day 5", "Day 4"], newest.Select(rite => rite.Title));
    }

    [Fact]
    public async Task Timestamps_ComeBackAsUtc_EvenWhenGivenAsLocalTime()
    {
        var utc = new DateTime(2026, 10, 7, 0, 0, 5, DateTimeKind.Utc);
        var rite = MakeRite(new DateOnly(2026, 10, 7), "Timed") with { GeneratedAtUtc = utc.ToLocalTime() };
        await AddOrFail(rite);
        var scoredAt = new DateTime(2026, 10, 7, 0, 0, 9, DateTimeKind.Utc);
        await Repository.SaveScores(rite.PublishedOnUtc,
            new RiteSimilarity { ScoresGeneratorVersion = "jev/v1", Scores = [1f], CreatedAtUtc = scoredAt.ToLocalTime() },
            CancellationToken.None);

        var saved = await PublishedOnOrFail(rite.PublishedOnUtc);

        Assert.Equal(utc, saved.GeneratedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved.GeneratedAtUtc.Kind);
        Assert.Equal(Some(scoredAt), saved.Similarity.Map(similarity => similarity.CreatedAtUtc));
        Assert.Equal(Some(DateTimeKind.Utc), saved.Similarity.Map(similarity => similarity.CreatedAtUtc.Kind));
    }

    [Fact]
    public async Task Add_WithScoresAlreadyAttached_StoresTheirTimestampAsUtc()
    {
        var scoredAt = new DateTime(2026, 10, 7, 0, 0, 9, DateTimeKind.Utc);
        var rite = MakeRite(new DateOnly(2026, 10, 7), "Scored first") with
        {
            Similarity = new RiteSimilarity { ScoresGeneratorVersion = "jev/v1", Scores = [0.5f], CreatedAtUtc = scoredAt.ToLocalTime() },
        };

        await AddOrFail(rite);
        var saved = await PublishedOnOrFail(rite.PublishedOnUtc);

        Assert.Equal(Some(scoredAt), saved.Similarity.Map(similarity => similarity.CreatedAtUtc));
        Assert.Equal(Some(DateTimeKind.Utc), saved.Similarity.Map(similarity => similarity.CreatedAtUtc.Kind));
    }

    [Fact]
    public async Task SaveScores_StoresTheScoresExactly_AndReplacesOlderOnes()
    {
        var rite = await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "Scored"));
        var createdAt = new DateTime(2026, 10, 7, 0, 1, 0, DateTimeKind.Utc);

        await Repository.SaveScores(rite.PublishedOnUtc,
            new RiteSimilarity { ScoresGeneratorVersion = "jev/old", Scores = [1f, 0f], CreatedAtUtc = createdAt }, CancellationToken.None);
        await Repository.SaveScores(rite.PublishedOnUtc,
            new RiteSimilarity { ScoresGeneratorVersion = "jev/new", Scores = [0.25f, 0.5f, 0.1f], CreatedAtUtc = createdAt }, CancellationToken.None);

        Assert.Empty(await Repository.GetScoresByRiteNumber("jev/old", CancellationToken.None));
        var scores = await Repository.GetScoresByRiteNumber("jev/new", CancellationToken.None);
        Assert.Equal([0.25f, 0.5f, 0.1f], scores[rite.Number]);
    }

    [Fact]
    public async Task GetScoresByRiteNumber_SkipsRitesWithoutScores()
    {
        await AddOrFail(MakeRite(new DateOnly(2026, 10, 7), "Unscored"));

        Assert.Empty(await Repository.GetScoresByRiteNumber("jev/v1", CancellationToken.None));
    }
}
