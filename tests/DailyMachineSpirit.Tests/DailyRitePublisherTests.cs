using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions;
using DailyMachineSpirit.Functions.Generation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

/// <summary>The daily rite end to end, minus the models and Jev: a real repository in the Cosmos DB emulator.</summary>
public sealed class DailyRitePublisherTests : IAsyncLifetime
{
    private const string Sol = "gpt-6-sol";
    private const string Luna = "gpt-6-luna";

    private static readonly DateOnly Today = new(2026, 10, 7);
    private static readonly string GoodAnswer = FakeChatClients.Answer(
        "The Rite of Re-Run", "Press Re-run thrice, O Machine Spirit.", "The test is flaky: fix its race instead.");

    private readonly CosmosTestContainer cosmos = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 0, 0, 5, TimeSpan.Zero));
    private readonly FakeChatClients models = new();
    private FakeJev jev = FakeJev.AnsweringBuildsAndRepetition();

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task PublishToday_WritesSavesAndScoresTodaysRite()
    {
        models.Answers(Sol, GoodAnswer);

        var published = Ok(await Publisher().PublishToday(CancellationToken.None));

        Assert.True(published.IsNew);
        var saved = await SavedOn(Today);
        Assert.Equal(1, saved.Number);
        Assert.Equal("The Rite of Re-Run", saved.Title);
        Assert.Equal(DailyRitePublisher.KindFor(Today), saved.Kind);
        Assert.Equal(Sol, saved.GeneratedByModel);
        var similarity = saved.Similarity.IfNone(() => throw new Xunit.Sdk.XunitException("Expected scores."));
        Assert.Equal("jev-1.13.0/q1", similarity.ScoresGeneratorVersion);
        Assert.Equal(1.0f, similarity.Scores[0]);
        Assert.Equal(similarity.Scores, published.Rite.Similarity.Map(returned => returned.Scores).IfNone([]));
    }

    [Fact]
    public async Task PublishToday_WhenTodayHasItsRite_KeepsIt_AndAsksNoModel()
    {
        await Repository.Add(Rite(Today, "Already here"), CancellationToken.None);

        var published = Ok(await Publisher().PublishToday(CancellationToken.None));

        Assert.False(published.IsNew);
        Assert.Equal("Already here", published.Rite.Title);
        Assert.Empty(models.Requests);
    }

    [Fact]
    public async Task PublishToday_ListsTheRecentTitles_ForTheModelToAvoid()
    {
        await Repository.Add(Rite(Today.AddDays(-2), "Litany of the Clean Cache"), CancellationToken.None);
        await Repository.Add(Rite(Today.AddDays(-1), "The Sacred Restart"), CancellationToken.None);
        models.Answers(Sol, GoodAnswer);

        await Publisher().PublishToday(CancellationToken.None);

        var instructions = models.Requests.Single().Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("* The Sacred Restart", instructions);
        Assert.Contains("* Litany of the Clean Cache", instructions);
    }

    [Fact]
    public async Task PublishToday_WhenScoringFails_StillPublishes_WithoutScores()
    {
        jev = new FakeJev(HttpStatusCode.PaymentRequired, """{"error": "Out of credits."}""");
        models.Answers(Sol, GoodAnswer);

        var published = Ok(await Publisher().PublishToday(CancellationToken.None));

        Assert.True(published.IsNew);
        Assert.True((await SavedOn(Today)).Similarity.IsNone);
    }

    [Fact]
    public async Task PublishToday_WhenNoModelWrites_FailsAndSavesNothing()
    {
        models.Fails(Sol, 3).Fails(Luna, 3);

        var error = Failed(await Publisher().PublishToday(CancellationToken.None));

        Assert.Contains("No model wrote", error.Message);
        Assert.True(Ok(await Repository.GetPublishedOn(Today, CancellationToken.None)).IsNone);
    }

    [Fact]
    public async Task PublishToday_WhenAnotherRunSavesFirst_KeepsTheirs()
    {
        models.Then(Sol,
        [
            async () =>
            {
                await Repository.Add(Rite(Today, "Saved by another run"), CancellationToken.None);
                return GoodAnswer;
            },
        ]);

        var published = Ok(await Publisher().PublishToday(CancellationToken.None));

        Assert.False(published.IsNew);
        Assert.Equal("Saved by another run", published.Rite.Title);
        Assert.Equal("Saved by another run", (await SavedOn(Today)).Title);
    }

    [Fact]
    public void KindFor_AlternatesDayByDay()
    {
        Assert.NotEqual(DailyRitePublisher.KindFor(Today), DailyRitePublisher.KindFor(Today.AddDays(1)));
        Assert.Equal(DailyRitePublisher.KindFor(Today), DailyRitePublisher.KindFor(Today.AddDays(2)));
    }

    [Fact]
    public async Task TimerFunction_PublishesTodaysRite()
    {
        models.Answers(Sol, GoodAnswer);

        await Function().Run(new TimerInfo(), CancellationToken.None);

        Assert.Equal("The Rite of Re-Run", (await SavedOn(Today)).Title);
    }

    [Fact]
    public async Task TimerFunction_WhenPublishingFails_Throws_SoTheRunCountsAsFailed()
    {
        models.Fails(Sol, 3).Fails(Luna, 3);

        await Assert.ThrowsAnyAsync<Exception>(() => Function().Run(new TimerInfo(), CancellationToken.None));
    }

    private RiteRepository Repository => new(cosmos.Container);

    private DailyRitePublisher Publisher()
    {
        var options = Options.Create(new GenerationOptions { RetryDelaySeconds = 0 });
        return new DailyRitePublisher(
            Repository,
            new RiteWriter(models, options, time, NullLogger<RiteWriter>.Instance),
            jev.Scorer(time, FakeJev.ApiKey),
            options,
            time,
            NullLogger<DailyRitePublisher>.Instance);
    }

    private DailyRiteFunction Function() => new(Publisher(), NullLogger<DailyRiteFunction>.Instance);

    private async Task<Rite> SavedOn(DateOnly day)
        => Ok(await Repository.GetPublishedOn(day, CancellationToken.None))
            .IfNone(() => throw new Xunit.Sdk.XunitException($"Expected a rite on {day}."));

    private static Rite Rite(DateOnly day, string title) => new()
    {
        PublishedOnUtc = day,
        Kind = DailyRitePublisher.KindFor(day),
        Title = title,
        Text = "Clear the cache, and the cache shall clear thee.",
        HereticalTruth = "A stale cache hides a missing invalidation.",
        GeneratedByModel = Sol,
        GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
    };
}
