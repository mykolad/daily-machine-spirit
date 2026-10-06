using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;

namespace DailyMachineSpirit.Tests;

public sealed class ItemRepositoryTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();
    private ItemRepository repository = null!;

    public async Task InitializeAsync()
    {
        await cosmos.InitializeAsync();
        repository = new ItemRepository(cosmos.Container);
    }

    public Task DisposeAsync() => cosmos.DisposeAsync();

    private static Item MakeItem(DateOnly date, string title) => new()
    {
        PublishedOnUtc = date,
        Kind = ItemKind.Ritual,
        Title = title,
        Text = "Press Re-run thrice, intoning `it passed locally`.",
        HereticalTruth = "Your test depends on timing.",
        GeneratedByModel = "gpt-6-sol",
        GeneratedAtUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
    };

    [Fact]
    public async Task TryAdd_SavesTheItem_ThenFindsItByNumberAndDay()
    {
        var date = new DateOnly(2026, 10, 7);
        var item = MakeItem(date, "The Rite of Re-Run");

        Assert.True(await repository.TryAdd(item, CancellationToken.None));

        var byNumber = await repository.GetByNumber(item.Number, CancellationToken.None);
        var byDay = await repository.GetPublishedOn(date, CancellationToken.None);
        Assert.Equal("The Rite of Re-Run", byNumber?.Title);
        Assert.Equal(ItemKind.Ritual, byNumber?.Kind);
        Assert.Equal("Press Re-run thrice, intoning `it passed locally`.", byDay?.Text);
        Assert.Equal(item.Number, byDay?.Number);
    }

    [Fact]
    public async Task TryAdd_NumbersItemsOneAfterAnother()
    {
        var first = MakeItem(new DateOnly(2026, 10, 7), "First");
        var second = MakeItem(new DateOnly(2026, 10, 8), "Second");

        await repository.TryAdd(first, CancellationToken.None);
        await repository.TryAdd(second, CancellationToken.None);

        Assert.Equal(1, first.Number);
        Assert.Equal(2, second.Number);
    }

    [Fact]
    public async Task TryAdd_ForADayThatHasAnItem_SavesNothing_AndUsesNoNumber()
    {
        var date = new DateOnly(2026, 10, 7);
        await repository.TryAdd(MakeItem(date, "First"), CancellationToken.None);

        var added = await repository.TryAdd(MakeItem(date, "Second"), CancellationToken.None);
        var next = MakeItem(date.AddDays(1), "Next day");
        await repository.TryAdd(next, CancellationToken.None);

        Assert.False(added);
        Assert.Equal("First", (await repository.GetPublishedOn(date, CancellationToken.None))?.Title);
        Assert.Equal(2, next.Number);
    }

    [Fact]
    public async Task TryAdd_ManyAtOnce_GivesEachDayItsOwnNumber()
    {
        var items = Enumerable.Range(1, 4).Select(day => MakeItem(new DateOnly(2026, 10, day), $"Day {day}")).ToList();

        var added = await Task.WhenAll(items.Select(i => repository.TryAdd(i, CancellationToken.None)));

        Assert.All(added, Assert.True);
        Assert.Equal([1, 2, 3, 4], items.Select(i => i.Number).Order());
    }

    [Fact]
    public async Task GetByNumber_AndGetPublishedOn_ReturnNullWhenMissing()
    {
        Assert.Null(await repository.GetByNumber(42, CancellationToken.None));
        Assert.Null(await repository.GetPublishedOn(new DateOnly(2026, 1, 1), CancellationToken.None));
    }

    [Fact]
    public async Task GetNewest_ReturnsTheNewestFirst()
    {
        foreach (var day in new[] { 3, 5, 4 })
            await repository.TryAdd(MakeItem(new DateOnly(2026, 10, day), $"Day {day}"), CancellationToken.None);

        var newest = await repository.GetNewest(2, CancellationToken.None);

        Assert.Equal(["Day 5", "Day 4"], newest.Select(i => i.Title));
    }

    [Fact]
    public async Task Timestamps_ComeBackAsUtc_EvenWhenGivenAsLocalTime()
    {
        var utc = new DateTime(2026, 10, 7, 0, 0, 5, DateTimeKind.Utc);
        var item = MakeItem(new DateOnly(2026, 10, 7), "Timed");
        item.GeneratedAtUtc = utc.ToLocalTime();
        await repository.TryAdd(item, CancellationToken.None);
        var scoredAt = new DateTime(2026, 10, 7, 0, 0, 9, DateTimeKind.Utc);
        await repository.SaveScores(item.PublishedOnUtc,
            new ItemSimilarity { ScoresGeneratorVersion = "jev/v1", Scores = [1f], CreatedAtUtc = scoredAt.ToLocalTime() },
            CancellationToken.None);

        var saved = await repository.GetPublishedOn(item.PublishedOnUtc, CancellationToken.None);

        Assert.Equal(utc, saved?.GeneratedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved?.GeneratedAtUtc.Kind);
        Assert.Equal(scoredAt, saved?.Similarity?.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved?.Similarity?.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task TryAdd_WithScoresAlreadyAttached_StoresTheirTimestampAsUtc()
    {
        var scoredAt = new DateTime(2026, 10, 7, 0, 0, 9, DateTimeKind.Utc);
        var item = MakeItem(new DateOnly(2026, 10, 7), "Scored first");
        item.Similarity = new ItemSimilarity { ScoresGeneratorVersion = "jev/v1", Scores = [0.5f], CreatedAtUtc = scoredAt.ToLocalTime() };

        await repository.TryAdd(item, CancellationToken.None);
        var saved = await repository.GetPublishedOn(item.PublishedOnUtc, CancellationToken.None);

        Assert.Equal(scoredAt, saved?.Similarity?.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved?.Similarity?.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task SaveScores_StoresTheScoresExactly_AndReplacesOlderOnes()
    {
        var item = MakeItem(new DateOnly(2026, 10, 7), "Scored");
        await repository.TryAdd(item, CancellationToken.None);
        var createdAt = new DateTime(2026, 10, 7, 0, 1, 0, DateTimeKind.Utc);

        await repository.SaveScores(item.PublishedOnUtc,
            new ItemSimilarity { ScoresGeneratorVersion = "jev/old", Scores = [1f, 0f], CreatedAtUtc = createdAt }, CancellationToken.None);
        await repository.SaveScores(item.PublishedOnUtc,
            new ItemSimilarity { ScoresGeneratorVersion = "jev/new", Scores = [0.25f, 0.5f, 0.1f], CreatedAtUtc = createdAt }, CancellationToken.None);

        Assert.Empty(await repository.GetScoresByItemNumber("jev/old", CancellationToken.None));
        var scores = await repository.GetScoresByItemNumber("jev/new", CancellationToken.None);
        Assert.Equal([0.25f, 0.5f, 0.1f], scores[item.Number]);
    }

    [Fact]
    public async Task GetScoresByItemNumber_SkipsItemsWithoutScores()
    {
        await repository.TryAdd(MakeItem(new DateOnly(2026, 10, 7), "Unscored"), CancellationToken.None);

        Assert.Empty(await repository.GetScoresByItemNumber("jev/v1", CancellationToken.None));
    }
}
