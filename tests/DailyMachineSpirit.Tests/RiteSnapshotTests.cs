using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Snapshot;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// The rites' snapshot and the fallback onto it while Cosmos DB doesn't answer (#112), against the Cosmos DB emulator:
/// the snapshot must answer every read as Cosmos DB does, so the pages look the same from either.
/// </summary>
public sealed class RiteSnapshotTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 10, 20);

    private readonly CosmosTestContainer cosmos = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero));
    private readonly InMemoryRiteSnapshot snapshot = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task TheSnapshot_AnswersEveryRead_AsCosmosDoes()
    {
        await AddRites();
        var copy = new SnapshotRites(Ok(await Rites.GetAllPublished(CancellationToken.None)));
        var none = CancellationToken.None;

        foreach (var source in new IRiteRepository[] { Rites, copy })
        {
            Assert.Equal([4, 3, 2], Numbers(await source.GetNewest(3, none)));
            Assert.Equal([4, 3, 2, 1], Numbers(await source.GetAllPublished(none)));
            Assert.Equal([1, 3], Numbers(await source.GetByNumbers([1, 3, 9], none)).Order());
            Assert.Equal([2, 1], Numbers(await source.GetOlderThan(3, 5, none)));
            Assert.Equal([3, 1], Numbers(await source.SearchOlderThan(["CACHE"], None, 5, 5, none)));
            Assert.Equal([1], Numbers(await source.SearchOlderThan(["cache", "warm"], Some("caches"), 5, 5, none)));
            Assert.Equal(["3:2026-10-19:caches", "1:2026-10-17:caches builds"],
                Ok(await source.FindDaysAndTags(["cache"], None, none)).Select(found => $"{found.Number}:{found.PublishedOnUtc:yyyy-MM-dd}:{string.Join(' ', found.Tags)}"));
            Assert.Equal([4, 3, 2, 1], Ok(await source.GetDays(none)).Select(day => day.Number));
            Assert.Equal(["The Rite of 4", "The Rite of 3", "The Rite of 2", "The Litany of the Warm Cache"], Ok(await source.GetAllWords(none)).Select(words => words.Title));
            Assert.Equal([0, 1, 0, 2], Ok(await source.GetAllUnrevealed(none)).Select(rite => rite.Tags.Count));
            Assert.Equal(Some("Truth of 2."), Ok(await source.GetHereticalTruth(2, none)));
            Assert.Equal(None, Ok(await source.GetHereticalTruth(9, none)));
            Assert.Equal([2, 4], Ok(await source.GetScoresByRiteNumber("jev/q1", none)).Keys.Order());
            Assert.Equal([3], Numbers(await source.GetTaggedOlderThan("caches", 4, 1, none)));
            Assert.Equal(Some(2), Ok(await source.GetPublishedOn(Day.AddDays(-2), none)).Map(rite => rite.Number));
        }
    }

    [Fact]
    public async Task TheSnapshot_SavesNothing()
    {
        var copy = new SnapshotRites([]);

        Assert.Equal(SnapshotRites.ReadOnly, Failed(await copy.SaveTags(Day, ["caches"], CancellationToken.None)));
        Assert.Equal(SnapshotRites.ReadOnly, Failed(await copy.Add(new Rite(), CancellationToken.None)));
        Assert.Equal(SnapshotRites.ReadOnly, Failed(await copy.Publish(Guid.NewGuid(), Day, CancellationToken.None)));
        Assert.Equal(SnapshotRites.ReadOnly, Failed(await copy.SaveScores(Day, new RiteSimilarity(), CancellationToken.None)));
        Assert.Equal(SnapshotRites.ReadOnly, Failed(await copy.GetUntagged(CancellationToken.None)));
    }

    // Cosmos DB down: the pages' reads come from the snapshot, which is loaded once rather than for every read, and again
    // once it's ten minutes old.
    [Fact]
    public async Task WhileCosmosFails_ReadsComeFromTheSnapshot()
    {
        await AddRites();
        Ok(await Snapshots(Rites).Write(CancellationToken.None));
        var rites = Fallback(Missing);

        var newest = await rites.GetNewest(2, CancellationToken.None);
        var found = await rites.SearchOlderThan(["cache"], None, 5, 5, CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(11));
        var later = await rites.GetByNumbers([1], CancellationToken.None);

        Assert.Equal([4, 3], Numbers(newest));
        Assert.Equal([3, 1], Numbers(found));
        Assert.Equal([1], Numbers(later));
        Assert.Equal(2, snapshot.Loads);
    }

    // Cosmos DB answering: the snapshot isn't even read; and a failure a minute old no longer keeps reads from Cosmos DB.
    [Fact]
    public async Task WhileCosmosAnswers_ReadsComeFromIt()
    {
        await AddRites();
        var rites = Fallback(Rites);

        var newest = await rites.GetNewest(1, CancellationToken.None);

        Assert.Equal([4], Numbers(newest));
        Assert.Equal(0, snapshot.Loads);
    }

    [Fact]
    public async Task WithoutASnapshot_ACosmosFailureIsTheReadsFailure()
    {
        var rites = Fallback(Missing);

        Assert.True((await rites.GetNewest(1, CancellationToken.None)).IsLeft);
        // Within the minute it isn't asked again, and there's still no snapshot to read.
        Assert.True((await rites.GetDays(CancellationToken.None)).IsLeft);
    }

    // Saving and the reads that lead to saving never use the copy.
    [Fact]
    public async Task Saving_AlwaysGoesToCosmos()
    {
        await AddRites();
        Ok(await Snapshots(Rites).Write(CancellationToken.None));
        var rites = Fallback(Missing);

        Assert.True((await rites.GetUntagged(CancellationToken.None)).IsLeft);
        Assert.True((await rites.SaveTags(Day, ["caches"], CancellationToken.None)).IsLeft);
        Assert.True((await rites.SaveScores(Day, new RiteSimilarity(), CancellationToken.None)).IsLeft);
        Assert.True((await rites.Add(new Rite { PublishedOnUtc = Day.AddDays(1) }, CancellationToken.None)).IsLeft);
        Assert.True((await rites.Publish(Guid.NewGuid(), Day.AddDays(1), CancellationToken.None)).IsLeft);
        Assert.True((await rites.GetAllPublished(CancellationToken.None)).IsLeft);
    }

    [Fact]
    public async Task Write_KeepsEveryPublishedRite()
    {
        await AddRites();

        var written = Ok(await Snapshots(Rites).Write(CancellationToken.None));

        Assert.Equal(4, written);
        Assert.Equal([4, 3, 2, 1], snapshot.Saved.IfNone([]).Select(rite => rite.Number));
    }

    [Fact]
    public async Task Write_WhenCosmosFails_SavesNothing()
    {
        var written = await Snapshots(Missing).Write(CancellationToken.None);

        Assert.True(written.IsLeft);
        Assert.True(snapshot.Saved.IsNone);
    }

    [Fact]
    public async Task TheTimer_WritesTheSnapshot()
    {
        await AddRites();

        await new RiteSnapshotFunction(Snapshots(Rites)).Run(new Microsoft.Azure.Functions.Worker.TimerInfo(), CancellationToken.None);

        Assert.Equal(4, snapshot.Saved.IfNone([]).Count);
    }

    private RiteRepository Rites => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private RiteRepository Missing => new(cosmos.Container.Database.GetContainer("no-such-container"), NullLogger<RiteRepository>.Instance);

    private RitesWithFallback Fallback(IRiteRepository rites) => new(rites, snapshot, time, NullLogger<RitesWithFallback>.Instance);

    private RiteSnapshots Snapshots(IRiteRepository rites) => new(rites, snapshot, NullLogger<RiteSnapshots>.Instance);

    private static List<int> Numbers(Either<Error, List<Rite>> rites) => Ok(rites).Select(rite => rite.Number).ToList();

    // NO. 1 is about a warm cache (caches and builds), NO. 3 about a cache (caches); NO. 2 and 4 have scores.
    private async Task AddRites()
    {
        await Add(Day.AddDays(-3), "The Litany of the Warm Cache", "Keep the cache warm.", Some(new[] { "caches", "builds" }), None);
        await Add(Day.AddDays(-2), "The Rite of 2", "Text of 2.", None, Some("jev/q1"));
        await Add(Day.AddDays(-1), "The Rite of 3", "The cache lies.", Some(new[] { "caches" }), None);
        await Add(Day, "The Rite of 4", "Text of 4.", None, Some("jev/q1"));
    }

    private async Task Add(DateOnly day, string title, string text, Option<string[]> tags, Option<string> scored)
        => Ok(await Rites.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting { Kind = RiteKind.Prayer, Title = title, Text = text, HereticalTruth = $"Truth of {title[^1]}." },
            Tags = tags,
            Similarity = scored.Map(version => new RiteSimilarity { ScoresGeneratorVersion = version, Scores = [1f, 0f], CreatedAtUtc = DateTime.UtcNow }),
        }, CancellationToken.None));
}
