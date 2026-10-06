using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;

namespace DailyMachineSpirit.Tests;

public sealed class ItemRepositoryTests : IDisposable
{
    private readonly TestDatabase database = new();

    public void Dispose() => database.Dispose();

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

    private ItemRepository CreateRepository() => new(database.CreateContext());

    [Fact]
    public async Task TryAdd_SavesTheItem_ThenFindsItByIdAndDay()
    {
        var date = new DateOnly(2026, 10, 7);
        var item = MakeItem(date, "The Rite of Re-Run");

        Assert.True(await CreateRepository().TryAdd(item, CancellationToken.None));

        var byId = await CreateRepository().GetById(item.Id, CancellationToken.None);
        var byDate = await CreateRepository().GetPublishedOn(date, CancellationToken.None);
        Assert.Equal("The Rite of Re-Run", byId?.Title);
        Assert.Equal(ItemKind.Ritual, byId?.Kind);
        Assert.Equal(item.Id, byDate?.Id);
    }

    [Fact]
    public async Task Timestamps_ComeBackAsUtc()
    {
        var item = MakeItem(new DateOnly(2026, 10, 7), "Timed");
        item.GeneratedAtUtc = new DateTime(2026, 10, 7, 0, 0, 5, DateTimeKind.Utc);
        await CreateRepository().TryAdd(item, CancellationToken.None);
        var scoredAt = new DateTime(2026, 10, 7, 0, 0, 9, DateTimeKind.Utc);
        await CreateRepository().SaveScores(item.Id, "jev/v1", [1f], scoredAt, CancellationToken.None);

        var saved = await CreateRepository().GetById(item.Id, CancellationToken.None);
        using var context = database.CreateContext();
        var profile = context.ItemProfiles.Single();

        Assert.Equal(DateTimeKind.Utc, saved?.GeneratedAtUtc.Kind);
        Assert.Equal(item.GeneratedAtUtc, saved?.GeneratedAtUtc);
        Assert.Equal(DateTimeKind.Utc, profile.CreatedAtUtc.Kind);
        Assert.Equal(scoredAt, profile.CreatedAtUtc);
    }

    [Fact]
    public async Task Timestamps_GivenAsLocalTime_AreStoredAsUtc()
    {
        var utc = new DateTime(2026, 10, 7, 0, 0, 5, DateTimeKind.Utc);
        var item = MakeItem(new DateOnly(2026, 10, 7), "Local");
        item.GeneratedAtUtc = utc.ToLocalTime();
        await CreateRepository().TryAdd(item, CancellationToken.None);

        var saved = await CreateRepository().GetById(item.Id, CancellationToken.None);

        Assert.Equal(utc, saved?.GeneratedAtUtc);
        Assert.Equal(DateTimeKind.Utc, saved?.GeneratedAtUtc.Kind);
    }

    [Fact]
    public async Task TryAdd_ForADateThatHasAnItem_SavesNothing()
    {
        var date = new DateOnly(2026, 10, 7);
        await CreateRepository().TryAdd(MakeItem(date, "First"), CancellationToken.None);

        var added = await CreateRepository().TryAdd(MakeItem(date, "Second"), CancellationToken.None);

        Assert.False(added);
        Assert.Equal("First", (await CreateRepository().GetPublishedOn(date, CancellationToken.None))?.Title);
    }

    [Fact]
    public async Task TryAdd_RethrowsAFailureThatIsNotTheDate()
    {
        var item = MakeItem(new DateOnly(2026, 10, 7), "No model");
        item.GeneratedByModel = null!;

        await Assert.ThrowsAnyAsync<Exception>(() => CreateRepository().TryAdd(item, CancellationToken.None));
    }

    [Fact]
    public async Task GetById_AndGetPublishedOn_ReturnNullWhenMissing()
    {
        Assert.Null(await CreateRepository().GetById(42, CancellationToken.None));
        Assert.Null(await CreateRepository().GetPublishedOn(new DateOnly(2026, 1, 1), CancellationToken.None));
    }

    [Fact]
    public async Task GetNewest_ReturnsTheNewestFirst()
    {
        foreach (var day in new[] { 3, 5, 4 })
            await CreateRepository().TryAdd(MakeItem(new DateOnly(2026, 10, day), $"Day {day}"), CancellationToken.None);

        var recent = await CreateRepository().GetNewest(2, CancellationToken.None);

        Assert.Equal(["Day 5", "Day 4"], recent.Select(i => i.Title));
    }

    [Fact]
    public async Task SaveScores_StoresTheScoresExactly_AndReplacesOlderOnes()
    {
        var item = MakeItem(new DateOnly(2026, 10, 7), "Profiled");
        await CreateRepository().TryAdd(item, CancellationToken.None);
        var createdAt = new DateTime(2026, 10, 7, 0, 1, 0, DateTimeKind.Utc);

        await CreateRepository().SaveScores(item.Id, "jev/old", [1f, 0f], createdAt, CancellationToken.None);
        await CreateRepository().SaveScores(item.Id, "jev/new", [0.25f, 0.5f, 0.125f], createdAt, CancellationToken.None);

        Assert.Empty(await CreateRepository().GetScoresByItemId("jev/old", CancellationToken.None));
        var profiles = await CreateRepository().GetScoresByItemId("jev/new", CancellationToken.None);
        Assert.Equal([0.25f, 0.5f, 0.125f], profiles[item.Id]);
    }

    [Fact]
    public async Task Scores_ChangedInPlace_AreSaved()
    {
        var item = MakeItem(new DateOnly(2026, 10, 7), "Profiled");
        await CreateRepository().TryAdd(item, CancellationToken.None);
        await CreateRepository().SaveScores(item.Id, "jev/v1", [1f, 2f], DateTime.UtcNow, CancellationToken.None);

        using (var context = database.CreateContext())
        {
            var profile = context.ItemProfiles.Single();
            profile.Scores[0] = 9f;
            await context.SaveChangesAsync();
        }

        var profiles = await CreateRepository().GetScoresByItemId("jev/v1", CancellationToken.None);
        Assert.Equal([9f, 2f], profiles[item.Id]);
    }
}
