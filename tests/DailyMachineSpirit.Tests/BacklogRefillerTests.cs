using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static DailyMachineSpirit.Tests.ScriptoriumRequests;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class BacklogRefillerTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task Refill_WritesJudgedDrafts_UntilTheBacklogIsFull()
    {
        await testbed.AddDraft("Already waiting", 0.5f);
        testbed.Models.Answers(Sol, Answer("Second"), Answer("Third"));

        var written = Ok(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.Equal(2, written);
        var waiting = await testbed.Waiting();
        Assert.Equal(["Already waiting", "Second", "Third"], waiting.Select(draft => draft.Writing.Title).Order());
        Assert.All(waiting, draft => Assert.True(draft.Augury.IsSome));
        Assert.All(waiting.Where(draft => draft.Writing.Title != "Already waiting"), draft => Assert.True(draft.Similarity.IsSome));
    }

    [Fact]
    public async Task Refill_WhenTheBacklogIsFull_WritesNothing()
    {
        foreach (var title in new[] { "One", "Two", "Three" })
        {
            await testbed.AddDraft(title, 0.5f);
        }

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

        var instructions = testbed.Models.Requests.Single(request => request.Model == Sol).Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("Write one RITUAL", instructions);
        Assert.Equal(RiteKind.Ritual, (await testbed.Waiting()).Single(draft => draft.Writing.Title == "Second").Writing.Kind);
    }

    [Fact]
    public async Task Refill_AsksForSubjects_NeitherTheRitesNorTheWaitingDraftsHave()
    {
        testbed.BacklogSize = 2;
        await testbed.AddRite(Today.AddDays(-1), "The Sacred Restart");
        await testbed.AddDraft("Litany of the Clean Cache", 0.5f);
        testbed.Models.Answers(Sol, Answer("Second"));

        await testbed.Refiller().Refill(CancellationToken.None);

        var instructions = testbed.Models.Requests.Single(request => request.Model == Sol).Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("* The Sacred Restart", instructions);
        Assert.Contains("* Litany of the Clean Cache", instructions);
    }

    // A new title on an old lesson is the same rite: it's written again, the writer now told of the one it repeated.
    [Fact]
    public async Task Refill_WritesAgain_WhenADraftRepeatsARite()
    {
        testbed.BacklogSize = 1;
        using var checks = testbed.Probe.Collect<long>("dms.generation.duplicate_checks");
        await testbed.AddRite(Today.AddDays(-1), "The Sacred Restart");
        testbed.Models.Answers(Sol, Answer("The Restart Renewed"), Answer("Litany of the Clean Cache"));
        testbed.Models.Answers(Luna, Repeats(1), Repeats(0));

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var saved = Assert.Single(await testbed.Waiting());
        Assert.Equal(("Litany of the Clean Cache", Option<string>.None), (saved.Writing.Title, saved.Resembles));
        var rewrite = testbed.Models.Requests.Where(request => request.Model == Sol).Last().Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Equal(2, rewrite.Split("* The Sacred Restart:").Length - 1);
        Assert.Equal(["outcome=repeated", "outcome=unique"], MetricsProbe.Tags(checks, "outcome"));
    }

    // Still repeating once its rewrites run out, it's kept, marked with what it repeats, for the Scribes to judge.
    [Fact]
    public async Task Refill_KeepsADraftThatStillRepeats_MarkedForTheScribes()
    {
        testbed.BacklogSize = 1;
        await testbed.AddRite(Today.AddDays(-1), "The Sacred Restart");
        testbed.Models.Answers(Sol, Answer("Restart I"), Answer("Restart II"), Answer("Restart III"));
        testbed.Models.Answers(Luna, Repeats(1), Repeats(1), Repeats(1));

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var saved = Assert.Single(await testbed.Waiting());
        Assert.Equal(("Restart III", Some("The Sacred Restart")), (saved.Writing.Title, saved.Resembles));
        var html = Html(await new ScriptoriumFunction(testbed.Scribes(), testbed.Amendments(), testbed.JudgingAgain(), testbed.Audit, new EveryoneIsScribeSignIn(), testbed.Time, NullLogger<ScriptoriumFunction>.Instance)
            .Page(Get(""), CancellationToken.None), 200);
        Assert.Contains("""<p class="resembles">Much like “The Sacred Restart”, already written.</p>""", html);
    }

    // A rewrite that fails on every model keeps the repeating draft, marked, rather than losing it: tonight's rite may
    // depend on it.
    [Fact]
    public async Task Refill_KeepsARepeatingDraft_WhenItsRewriteFails()
    {
        testbed.BacklogSize = 1;
        await testbed.AddRite(Today.AddDays(-1), "The Sacred Restart");
        testbed.Models.Answers(Sol, Answer("Restart I")).Fails(Sol, 3);
        testbed.Models.Answers(Luna, Repeats(1)).Fails(Luna, 3);

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var saved = Assert.Single(await testbed.Waiting());
        Assert.Equal(("Restart I", Some("The Sacred Restart")), (saved.Writing.Title, saved.Resembles));
    }

    // Another refill (Summon twice) may save a draft while this one writes: the check reads the waiting drafts again.
    [Fact]
    public async Task Refill_ChecksAgainstADraftSavedWhileItWrote()
    {
        testbed.BacklogSize = 1;
        testbed.Models.Then(Sol,
        [
            async () =>
            {
                await testbed.AddDraft("Saved Meanwhile", 0.5f);
                return Answer("Mine");
            },
        ]);
        testbed.Models.Answers(Luna, Repeats(0));

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var check = testbed.Models.Requests.Single(request => request.Model == Luna).Messages.Single(message => message.Role == ChatRole.User).Text;
        Assert.Contains("Saved Meanwhile", check);
    }

    // The prompt lists the recent rites; the check reads every rite ever published, and the drafts too.
    [Fact]
    public async Task Refill_ChecksAgainstEveryPublishedRite_NotOnlyTheRecentOnes()
    {
        testbed.BacklogSize = 2;
        await testbed.AddRite(Today.AddDays(-BacklogRefiller.RecentRitesInPrompt - 1), "The Ancient Restart");
        for (var daysAgo = BacklogRefiller.RecentRitesInPrompt; daysAgo > 1; daysAgo--)
        {
            await testbed.AddRite(Today.AddDays(-daysAgo), $"Recent Rite {daysAgo}");
        }
        await testbed.AddRite(Today.AddDays(-1), "The Sacred Restart");
        await testbed.AddDraft("Litany of the Clean Cache", 0.5f);
        testbed.Models.Answers(Sol, Answer("Second"));
        testbed.Models.Answers(Luna, Repeats(0));

        await testbed.Refiller().Refill(CancellationToken.None);

        var prompt = testbed.Models.Requests.Single(request => request.Model == Sol).Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.DoesNotContain("The Ancient Restart", prompt);
        var check = testbed.Models.Requests.Single(request => request.Model == Luna).Messages.Single(message => message.Role == ChatRole.User).Text;
        Assert.Contains("The Ancient Restart", check);
        Assert.Contains("The Sacred Restart", check);
        Assert.Contains("Litany of the Clean Cache", check);
    }

    // Checking only guards the backlog: a draft the model couldn't check is kept, as an unjudged one is.
    [Theory]
    [InlineData("""{"sameAs": 7}""")]
    [InlineData("not json at all")]
    public async Task Refill_KeepsADraftThatCouldntBeChecked(string answer)
    {
        testbed.BacklogSize = 1;
        using var checks = testbed.Probe.Collect<long>("dms.generation.duplicate_checks");
        await testbed.AddRite(Today.AddDays(-1), "The Sacred Restart");
        testbed.Models.Answers(Sol, Answer("Second"));
        testbed.Models.Answers(Luna, answer);

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var saved = Assert.Single(await testbed.Waiting());
        Assert.Equal(("Second", Option<string>.None), (saved.Writing.Title, saved.Resembles));
        Assert.Equal(["outcome=failed"], MetricsProbe.Tags(checks, "outcome"));
    }

    [Fact]
    public async Task Refill_WhenJevFails_StillAddsTheDrafts_WithoutScores()
    {
        testbed.BacklogSize = 1;
        testbed.Jev = new FakeJev(HttpStatusCode.PaymentRequired, """{"error": "Out of credits."}""");
        testbed.Models.Answers(Sol, Answer("Unjudged"));
        using var judging = testbed.Probe.Collect<long>("dms.judging.runs");

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var draft = Assert.Single(await testbed.Waiting());
        Assert.True(draft.Augury.IsNone);
        Assert.True(draft.Similarity.IsNone);
        Assert.Equal(["outcome=failed"], MetricsProbe.Tags(judging, "outcome"));
    }

    [Fact]
    public async Task Refill_WithJudgingOff_AddsTheDrafts_WithoutScores()
    {
        testbed.BacklogSize = 1;
        testbed.JevApiKey = string.Empty;
        testbed.Models.Answers(Sol, Answer("Unjudged"));
        using var judging = testbed.Probe.Collect<long>("dms.judging.runs");

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.True(Assert.Single(await testbed.Waiting()).Augury.IsNone);
        Assert.Equal(["outcome=off"], MetricsProbe.Tags(judging, "outcome"));
    }

    [Fact]
    public async Task Refill_CountsEachDraftsJudging()
    {
        testbed.BacklogSize = 2;
        testbed.Models.Answers(Sol, Answer("First"), Answer("Second"));
        using var judging = testbed.Probe.Collect<long>("dms.judging.runs");

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.Equal(["outcome=judged", "outcome=judged"], MetricsProbe.Tags(judging, "outcome"));
    }

    [Fact]
    public async Task Refill_WhenTheModelsFail_ReturnsTheError_AndKeepsTheDraftsWrittenBefore()
    {
        testbed.Models.Answers(Sol, Answer("Written")).Fails(Sol, 3).Fails(Luna, 3);

        var error = Failed(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.Contains("No model wrote", error.Message);
        Assert.Equal("Written", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    [Fact]
    public async Task RefillFunction_RefillsTheBacklog()
    {
        testbed.BacklogSize = 1;
        testbed.Models.Answers(Sol, Answer("Summoned"));

        await Function().Run(RefillBacklogFunction.AfterPublishing, CancellationToken.None);

        Assert.Equal("Summoned", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    [Fact]
    public async Task RefillFunction_WhenRefillingFails_Throws_SoTheMessageIsTriedAgain()
    {
        testbed.Models.Fails(Sol, 3).Fails(Luna, 3);

        await Assert.ThrowsAnyAsync<Exception>(() => Function().Run(RefillBacklogFunction.AfterPublishing, CancellationToken.None));
    }

    private RefillBacklogFunction Function() => new(testbed.Refiller(), NullLogger<RefillBacklogFunction>.Instance);

    // The duplicate check's answer: the number of the rite the draft repeats, or 0.
    private static string Repeats(int number) => $$"""{"sameAs": {{number}}}""";
}
