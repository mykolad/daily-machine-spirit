using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public sealed class BacklogRefillerTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task Refill_WritesScoredDrafts_UntilTheBacklogIsFull()
    {
        await testbed.AddDraft("Already waiting", 0.5f);
        testbed.Models.Answers(Sol, Answer("Second"), Answer("Third"));

        var written = Ok(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.Equal(2, written);
        var waiting = await testbed.Waiting();
        Assert.Equal(["Already waiting", "Second", "Third"], waiting.Select(draft => draft.Title).Order());
        Assert.All(waiting, draft => Assert.True(draft.Augury.IsSome));
        Assert.All(waiting.Where(draft => draft.Title != "Already waiting"), draft => Assert.True(draft.Similarity.IsSome));
    }

    [Fact]
    public async Task Refill_WhenTheBacklogIsFull_WritesNothing()
    {
        foreach (var title in new[] { "One", "Two", "Three" })
            await testbed.AddDraft(title, 0.5f);

        var written = Ok(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.Equal(0, written);
        Assert.Empty(testbed.Models.Requests);
    }

    [Fact]
    public async Task Refill_WritesTheKindTheBacklogHasFewerOf()
    {
        testbed.BacklogSize = 2;
        await testbed.AddDraft("A prayer", 0.5f);
        testbed.Models.Answers(Sol, Answer("Second"));

        await testbed.Refiller().Refill(CancellationToken.None);

        var instructions = testbed.Models.Requests.Single().Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("Write one RITUAL", instructions);
        Assert.Equal(RiteKind.Ritual, (await testbed.Waiting()).Single(draft => draft.Title == "Second").Kind);
    }

    [Fact]
    public async Task Refill_AsksForSubjects_NeitherTheRitesNorTheWaitingDraftsHave()
    {
        testbed.BacklogSize = 2;
        await testbed.AddRite(Today.AddDays(-1), "The Sacred Restart");
        await testbed.AddDraft("Litany of the Clean Cache", 0.5f);
        testbed.Models.Answers(Sol, Answer("Second"));

        await testbed.Refiller().Refill(CancellationToken.None);

        var instructions = testbed.Models.Requests.Single().Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("* The Sacred Restart", instructions);
        Assert.Contains("* Litany of the Clean Cache", instructions);
    }

    [Fact]
    public async Task Refill_WhenJevFails_StillAddsTheDrafts_WithoutScores()
    {
        testbed.BacklogSize = 1;
        testbed.Jev = new FakeJev(HttpStatusCode.PaymentRequired, """{"error": "Out of credits."}""");
        testbed.Models.Answers(Sol, Answer("Unscored"));

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var draft = Assert.Single(await testbed.Waiting());
        Assert.True(draft.Augury.IsNone);
        Assert.True(draft.Similarity.IsNone);
    }

    [Fact]
    public async Task Refill_WhenTheModelsFail_ReturnsTheError_AndKeepsTheDraftsWrittenBefore()
    {
        testbed.Models.Answers(Sol, Answer("Written")).Fails(Sol, 3).Fails(Luna, 3);

        var error = Failed(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.Contains("No model wrote", error.Message);
        Assert.Equal("Written", Assert.Single(await testbed.Waiting()).Title);
    }

    [Fact]
    public async Task RefillFunction_RefillsTheBacklog()
    {
        testbed.BacklogSize = 1;
        testbed.Models.Answers(Sol, Answer("Summoned"));

        await Function().Run(RefillBacklogFunction.AfterPublishing, CancellationToken.None);

        Assert.Equal("Summoned", Assert.Single(await testbed.Waiting()).Title);
    }

    [Fact]
    public async Task RefillFunction_WhenRefillingFails_Throws_SoTheMessageIsTriedAgain()
    {
        testbed.Models.Fails(Sol, 3).Fails(Luna, 3);

        await Assert.ThrowsAnyAsync<Exception>(() => Function().Run(RefillBacklogFunction.AfterPublishing, CancellationToken.None));
    }

    private RefillBacklogFunction Function() => new(testbed.Refiller(), NullLogger<RefillBacklogFunction>.Instance);
}
