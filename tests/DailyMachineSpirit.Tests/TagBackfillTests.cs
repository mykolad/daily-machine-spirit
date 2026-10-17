using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Tags;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Tagging the rites and drafts judged before tags existed, in the Cosmos DB emulator.</summary>
public sealed class TagBackfillTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 10, 9);

    private readonly CosmosTestContainer cosmos = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 9, 20, 48, 0, TimeSpan.Zero));

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task Run_TagsOlderRitesAndDrafts_FromTheirScores()
    {
        await AddRite(Day.AddDays(-1), TopicScores.About(("caches", 0.7f), ("restarts", 0.3f)));
        var draft = await AddDraft(TopicScores.About(("ai", 0.9f)));

        var tagged = Ok(await Backfill().Run(CancellationToken.None));

        Assert.Equal(2, tagged);
        Assert.Equal(Some(new[] { "caches", "restarts" }), Ok(await Rites.GetByNumbers([1], CancellationToken.None))[0].Tags);
        Assert.Equal(Some(new[] { "ai" }), Ok(await Drafts.Get(draft.Id, CancellationToken.None)).Bind(found => found.Tags));
    }

    // An amendment saved between the backfill's read and its save: tagged anew from the new words, or left without
    // scores when judging them failed. Tags from the old scores mustn't overwrite either.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaleTags_DontOverwriteAnAmendmentSavedMeanwhile(bool judgedAgain)
    {
        var draft = await AddDraft(TopicScores.About(("ai", 0.9f)));
        var read = Assert.Single(Ok(await Drafts.GetUntagged(CancellationToken.None)));
        var amended = judgedAgain
            ? draft with { Tags = Some(new[] { "caches" }), Similarity = Some(TopicScores.About(("caches", 0.9f))) }
            : draft with { Similarity = None };
        await cosmos.Container.UpsertItemAsync(DraftDocument.From(amended), CosmosCalls.SharedPartitionKey);

        Ok(await Drafts.SaveTags(read.Id, ["ai"], CancellationToken.None));

        var tags = Ok(await Drafts.Get(draft.Id, CancellationToken.None)).Bind(found => found.Tags);
        Assert.Equal(judgedAgain ? Some(new[] { "caches" }) : None, tags);
    }

    // Another judge's scores stay untagged until a judge that reads them comes; the rest are tagged meanwhile.
    [Fact]
    public async Task Run_LeavesScoresTheJudgeCantRead()
    {
        await AddRite(Day.AddDays(-1), TopicScores.About(("caches", 0.9f)) with { ScoresGeneratorVersion = "other/q1" });
        await AddRite(Day, TopicScores.About(("data", 0.9f)));

        var tagged = Ok(await Backfill().Run(CancellationToken.None));

        Assert.Equal(1, tagged);
        Assert.Equal([1], Ok(await Rites.GetUntagged(CancellationToken.None)).Select(rite => rite.Number));
    }

    [Fact]
    public async Task Starting_TagsThem_AndAfterwardsNothingIsLeft()
    {
        await AddRite(Day, TopicScores.About(("security", 0.9f)));
        var backfill = Backfill();

        await backfill.StartAsync(CancellationToken.None);
        await (backfill.ExecuteTask ?? Task.CompletedTask);
        var again = Ok(await backfill.Run(CancellationToken.None));

        Assert.Equal(Some(new[] { "security" }), Ok(await Rites.GetByNumbers([1], CancellationToken.None))[0].Tags);
        Assert.Equal(0, again);
    }

    [Fact]
    public async Task Run_WhenCosmosFails_ReturnsTheError()
    {
        var missing = cosmos.Container.Database.GetContainer("no-such-container");
        var backfill = new TagBackfill(
            new RiteRepository(missing, NullLogger<RiteRepository>.Instance), new DraftRepository(missing), Judge(), NullLogger<TagBackfill>.Instance);

        Assert.True((await backfill.Run(CancellationToken.None)).IsLeft);
    }

    private RiteRepository Rites => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private DraftRepository Drafts => new(cosmos.Container);

    private TagBackfill Backfill() => new(Rites, Drafts, Judge(), NullLogger<TagBackfill>.Instance);

    // Reading tags from stored scores never asks Jev, so a Jev that can't answer does.
    private JevJudge Judge() => new FakeJev(HttpStatusCode.InternalServerError, "").NewJudge(time, FakeJev.ApiKey);

    private async Task AddRite(DateOnly day, RiteSimilarity similarity)
        => Ok(await Rites.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting { Kind = RiteKind.Ritual, Title = $"Rite of {day}", Text = "Clear it.", HereticalTruth = "It was stale." },
            Similarity = similarity,
        }, CancellationToken.None));

    private async Task<Draft> AddDraft(RiteSimilarity similarity)
        => Ok(await Drafts.Add(new Draft
        {
            Id = Guid.NewGuid(),
            State = DraftState.Waiting,
            Writing = new RiteWriting { Kind = RiteKind.Prayer, Title = "A waiting draft", Text = "O Machine Spirit.", HereticalTruth = "It's a model." },
            Similarity = similarity,
        }, CancellationToken.None));
}
