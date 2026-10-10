using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Drafts, and publishing one as the day's rite (<see cref="RiteRepository.Publish"/>), in the Cosmos DB emulator.</summary>
public sealed class DraftRepositoryTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 10, 7);

    private readonly CosmosTestContainer cosmos = new();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task Add_ThenGetWaiting_ReturnsTheDraftAsSaved()
    {
        var judgedAt = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);
        var draft = MakeDraft("The Rite of Re-Run") with
        {
            Similarity = new RiteSimilarity { ScoresGeneratorVersion = "jev/q1", Scores = [0.25f, 0.75f], CreatedAtUtc = judgedAt },
            Augury = new Augury { Quality = 0.8f, JudgedBy = "jev/quality-q1", JudgedAtUtc = judgedAt.ToLocalTime() },
        };

        Ok(await Drafts.Add(draft, CancellationToken.None));
        var saved = Assert.Single(Ok(await Drafts.GetWaiting(CancellationToken.None)));

        Assert.Equal(draft.Id, saved.Id);
        Assert.Equal(DraftState.Waiting, saved.State);
        Assert.Equal(RiteKind.Prayer, saved.Kind);
        Assert.Equal("The Rite of Re-Run", saved.Title);
        Assert.Equal(draft.GeneratedAtUtc, saved.GeneratedAtUtc);
        Assert.Equal(Some(0.8f), saved.Augury.Map(augury => augury.Quality));
        Assert.Equal(Some(judgedAt), saved.Augury.Map(augury => augury.JudgedAtUtc));
        Assert.Equal(Some(DateTimeKind.Utc), saved.Augury.Map(augury => augury.JudgedAtUtc.Kind));
        Assert.Equal([0.25f, 0.75f], saved.Similarity.Map(similarity => similarity.Scores).IfNone([]));
        Assert.True(saved.PublishedOnUtc.IsNone);
    }

    [Fact]
    public async Task Publish_MakesTheDraftTheDaysRite_WithTheNextNumber_AndItStopsWaiting()
    {
        Ok(await Rites.Add(MakeDraft("Yesterday's").ToRite(Day.AddDays(-1)), CancellationToken.None));
        var scores = new RiteSimilarity { ScoresGeneratorVersion = "jev/q1", Scores = [1f], CreatedAtUtc = DateTime.UtcNow };
        var draft = Ok(await Drafts.Add(MakeDraft("Today's") with { Similarity = scores }, CancellationToken.None));

        var rite = Ok(await Rites.Publish(draft.Id, Day, CancellationToken.None));

        Assert.Equal(2, rite.Number);
        var saved = Ok(await Rites.GetPublishedOn(Day, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("No rite."));
        Assert.Equal("Today's", saved.Title);
        Assert.Equal(RiteKind.Prayer, saved.Kind);
        Assert.Equal(draft.GeneratedByModel, saved.GeneratedByModel);
        Assert.Equal([1f], saved.Similarity.Map(similarity => similarity.Scores).IfNone([]));
        Assert.Empty(Ok(await Drafts.GetWaiting(CancellationToken.None)));
    }

    [Fact]
    public async Task Publish_TheSameDraftAgain_IsDraftNotWaiting()
    {
        var draft = Ok(await Drafts.Add(MakeDraft("Once only"), CancellationToken.None));
        Ok(await Rites.Publish(draft.Id, Day, CancellationToken.None));

        var again = await Rites.Publish(draft.Id, Day.AddDays(1), CancellationToken.None);

        Assert.Equal(RiteRepository.DraftNotWaiting, Failed(again));
        Assert.True(Ok(await Rites.GetPublishedOn(Day.AddDays(1), CancellationToken.None)).IsNone);
    }

    [Fact]
    public async Task Publish_ADraftThatDoesNotExist_IsDraftNotWaiting()
    {
        var result = await Rites.Publish(Guid.NewGuid(), Day, CancellationToken.None);

        Assert.Equal(RiteRepository.DraftNotWaiting, Failed(result));
    }

    [Fact]
    public async Task Publish_OnADayThatHasARite_IsDayAlreadyHasRite_AndTheDraftKeepsWaiting()
    {
        Ok(await Rites.Add(MakeDraft("Already here").ToRite(Day), CancellationToken.None));
        var draft = Ok(await Drafts.Add(MakeDraft("Too late"), CancellationToken.None));

        var result = await Rites.Publish(draft.Id, Day, CancellationToken.None);

        Assert.Equal(RiteRepository.DayAlreadyHasRite, Failed(result));
        Assert.Equal(draft.Id, Assert.Single(Ok(await Drafts.GetWaiting(CancellationToken.None))).Id);
    }

    [Fact]
    public async Task Publish_TheSameDraftOnTwoDaysAtOnce_PublishesItOnce()
    {
        var draft = Ok(await Drafts.Add(MakeDraft("Contested"), CancellationToken.None));

        var results = await Task.WhenAll(
            Rites.Publish(draft.Id, Day, CancellationToken.None),
            Rites.Publish(draft.Id, Day.AddDays(1), CancellationToken.None));

        Assert.Single(results, result => result.IsRight);
        Assert.Equal(RiteRepository.DraftNotWaiting, Failed(Assert.Single(results, result => result.IsLeft)));
    }

    private RiteRepository Rites => new(cosmos.Container, NullLogger<RiteRepository>.Instance);

    private DraftRepository Drafts => new(cosmos.Container);

    private static Draft MakeDraft(string title) => new()
    {
        Id = Guid.NewGuid(),
        State = DraftState.Waiting,
        Kind = RiteKind.Prayer,
        Title = title,
        Text = "O Machine Spirit, let the cache be warm.",
        HereticalTruth = "A cold cache is just a cache that hasn't been read yet.",
        GeneratedByModel = "gpt-6-sol",
        GeneratedAtUtc = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc),
    };
}
