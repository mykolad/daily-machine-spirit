using DailyMachineSpirit.Functions;
using DailyMachineSpirit.Functions.Generation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public sealed class DailyRitePublisherTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();
    private readonly FakePageCache pages = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task PublishToday_PublishesTheTopOfTheCalendar_AndTheRestKeepWaiting()
    {
        await testbed.AddDraft("Fair", 0.4f);
        await testbed.AddDraft("Excellent", 0.95f);

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.True(published.IsNew);
        Assert.Equal("Excellent", published.Rite.Writing.Title);
        Assert.Equal(1, published.Rite.Number);
        Assert.Equal("Excellent", (await testbed.PublishedOn(Today)).Writing.Title);
        Assert.Equal("Fair", Assert.Single(await testbed.Waiting()).Writing.Title);
        Assert.Empty(testbed.Models.Requests);
    }

    [Fact]
    public async Task PublishToday_CountsThePublishedRite_OnlyOnce()
    {
        using var published = testbed.Probe.Collect<long>("dms.rites.published");
        await testbed.AddDraft("Excellent", 0.95f);

        Ok(await testbed.Publisher().PublishToday(CancellationToken.None));
        Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.Equal([$"kind=prayer model={Sol}"], MetricsProbe.Tags(published, "kind", "model"));
    }

    [Fact]
    public async Task PublishToday_WhenTodayHasItsRite_KeepsIt_AndTheBacklogIsUntouched()
    {
        await testbed.AddRite(Today, "Already here");
        await testbed.AddDraft("Waiting", 0.9f);

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.False(published.IsNew);
        Assert.Equal("Already here", published.Rite.Writing.Title);
        Assert.Single(await testbed.Waiting());
        Assert.Empty(pages.Purges);
    }

    [Fact]
    public async Task PublishToday_WithAnEmptyBacklog_WritesTheRiteOnTheSpot()
    {
        testbed.Models.Answers(Sol, Answer("Written on the spot"));

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.True(published.IsNew);
        var rite = await testbed.PublishedOn(Today);
        Assert.Equal("Written on the spot", rite.Writing.Title);
        Assert.True(rite.Similarity.IsSome);
        Assert.Empty(await testbed.Waiting());
    }

    [Fact]
    public async Task PublishToday_WithAnEmptyBacklog_AndNoModelWriting_FailsAndPublishesNothing()
    {
        testbed.Models.Fails(Sol, 3).Fails(Luna, 3);

        var error = Failed(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.Contains("No model wrote", error.Message);
        Assert.True(Ok(await testbed.Rites.GetPublishedOn(Today, CancellationToken.None)).IsNone);
    }

    [Fact]
    public async Task PublishToday_WhenAnotherRunPublishesFirst_KeepsTheirs()
    {
        testbed.Models.Then(Sol,
        [
            async () =>
            {
                await testbed.AddRite(Today, "Published by another run");
                return Answer("Too late");
            },
        ]);

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.False(published.IsNew);
        Assert.Equal("Published by another run", (await testbed.PublishedOn(Today)).Writing.Title);
        Assert.Equal("Too late", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    [Fact]
    public async Task PublishToday_WhenItsFallbackFails_WhileAnotherRunPublishes_KeepsTheirs()
    {
        testbed.Models.Then(Sol,
        [
            async () =>
            {
                await testbed.AddRite(Today, "Published by another run");
                throw new HttpRequestException("The model is overloaded.");
            },
        ]).Fails(Sol, 2).Fails(Luna, 3);

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.False(published.IsNew);
        Assert.Equal("Published by another run", published.Rite.Writing.Title);
    }

    [Fact]
    public async Task PublishToday_AfterWritingAFallback_ChoosesFromTheCalendarAgain()
    {
        testbed.Models.Then(Sol,
        [
            async () =>
            {
                // A refill saves a better draft while the fallback is being written.
                await testbed.AddDraft("Better, from a refill", 0.95f);
                return Answer("The fallback");
            },
        ]);

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.Equal("Better, from a refill", published.Rite.Writing.Title);
        Assert.Equal("The fallback", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    [Fact]
    public async Task PublishToday_TwoRunsAtOnce_PublishOneRite_AndBothSucceed()
    {
        await testbed.AddDraft("The only draft", 0.9f);

        var results = await Task.WhenAll(
            testbed.Publisher().PublishToday(CancellationToken.None),
            testbed.Publisher().PublishToday(CancellationToken.None));

        var published = results.Select(Ok).ToList();
        Assert.Single(published, run => run.IsNew);
        Assert.All(published, run => Assert.Equal("The only draft", run.Rite.Writing.Title));
        Assert.Equal(1, (await testbed.PublishedOn(Today)).Number);
    }

    [Fact]
    public async Task TimerFunction_PublishesTodaysRite_AndAsksForARefill()
    {
        await testbed.AddDraft("Next up", 0.9f);

        var message = await Function().Run(new TimerInfo(), CancellationToken.None);

        Assert.Equal("Next up", (await testbed.PublishedOn(Today)).Writing.Title);
        Assert.Equal(RefillBacklogFunction.AfterPublishing, message);
    }

    // Today showed the rite before, and the search field's index lacked the new one, both kept at the edge until purged.
    [Fact]
    public async Task TimerFunction_PublishingANewRite_PurgesToday_AndTheSearchIndex()
    {
        await testbed.AddDraft("Next up", 0.9f);

        await Function().Run(new TimerInfo(), CancellationToken.None);

        Assert.Equal(["/", "/api/search-index"], Assert.Single(pages.Purges));
    }

    // A retry later in the day: the page already shows the day's rite.
    [Fact]
    public async Task TimerFunction_WhenTheDayAlreadyHasItsRite_PurgesNothing()
    {
        await testbed.AddDraft("Next up", 0.9f);
        await Function().Run(new TimerInfo(), CancellationToken.None);
        pages.Purges.Clear();

        await Function().Run(new TimerInfo(), CancellationToken.None);

        Assert.Empty(pages.Purges);
    }

    [Fact]
    public async Task TimerFunction_WhenPublishingFails_Throws_SoTheRunCountsAsFailed()
    {
        testbed.Models.Fails(Sol, 3).Fails(Luna, 3);

        await Assert.ThrowsAnyAsync<Exception>(() => Function().Run(new TimerInfo(), CancellationToken.None));
    }

    private DailyRiteFunction Function() => new(testbed.Publisher(), pages, NullLogger<DailyRiteFunction>.Instance);
}
