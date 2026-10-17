using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Judging;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Judging again the drafts judging couldn't score, against the Cosmos DB emulator with a fake Jev.</summary>
public sealed class JudgingAgainTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    // An outage that's over heals: the unjudged get their scores, quality and tags, recorded as the Machine Spirit's.
    [Fact]
    public async Task TheUnjudged_AreJudged_AndRecorded()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        var judged = await testbed.AddDraft("Already judged", 0.9f);

        Assert.Equal(1, Ok(await testbed.JudgingAgain().JudgeTheUnjudged(CancellationToken.None)));

        var saved = Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Gone."));
        Assert.True(saved.Augury.IsSome && saved.Similarity.IsSome && saved.Tags.IsSome);
        Assert.Equal(Some(0.9f), Ok(await testbed.Drafts.Get(judged.Id, CancellationToken.None)).Bind(draft => draft.Augury).Map(augury => augury.Quality));
        var entry = Assert.Single(Ok(await testbed.Audit.GetHistory(unjudged.Id, CancellationToken.None)), found => found.Action == AuditAction.JudgeAgain);
        Assert.Equal(AuditEntry.MachineSpirit, entry.Actor);
        Assert.StartsWith("the Augury: ", entry.Detail.IfNone(string.Empty));
    }

    // Jev still failing, or turned off: nothing is saved, and nothing recorded.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithoutAJudge_NothingChanges(bool judgeFailing)
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        if (judgeFailing)
        {
            testbed.Jev = new FakeJev(HttpStatusCode.InternalServerError, "");
        }
        else
        {
            testbed.JevApiKey = "";
        }

        Assert.Equal(0, Ok(await testbed.JudgingAgain().JudgeTheUnjudged(CancellationToken.None)));
        Assert.True(Ok(await testbed.JudgingAgain().JudgeAgain(unjudged.Id, Tester, CancellationToken.None)).IsNone);

        Assert.True(Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).Bind(draft => draft.Augury).IsNone);
        Assert.DoesNotContain(Ok(await testbed.Audit.GetHistory(unjudged.Id, CancellationToken.None)), entry => entry.Action == AuditAction.JudgeAgain);
    }

    [Fact]
    public async Task AJudgedDraft_HasNothingToJudge()
    {
        var judged = await testbed.AddDraft("Already judged", 0.9f);

        Assert.Equal(JudgingAgain.NothingToJudge, Failed(await testbed.JudgingAgain().JudgeAgain(judged.Id, Tester, CancellationToken.None)));
    }

    // Judged meanwhile (an amendment, or another refill): the later save mustn't overwrite it.
    [Fact]
    public async Task AJudgmentSavedMeanwhile_IsntOverwritten()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        var first = unjudged with { Augury = new Augury { Quality = 0.8f, JudgedBy = "first" } };
        var second = unjudged with { Augury = new Augury { Quality = 0.2f, JudgedBy = "second" } };
        Ok(await testbed.Drafts.SaveJudgmentIfUnjudged(first, Entry(unjudged), CancellationToken.None));

        var late = await testbed.Drafts.SaveJudgmentIfUnjudged(second, Entry(unjudged), CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(late));
        Assert.Equal(Some("first"), Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).Bind(draft => draft.Augury).Map(augury => augury.JudgedBy));
    }

    // Each refill judges the unjudged first, so an outage heals without a Scribe.
    [Fact]
    public async Task ARefill_JudgesTheUnjudgedFirst()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        testbed.BacklogSize = 1;

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.True(Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).Bind(draft => draft.Augury).IsSome);
    }

    private static AuditEntry Entry(Draft draft)
        => new() { Id = Guid.NewGuid(), Actor = Tester, Action = AuditAction.JudgeAgain, DraftId = draft.Id, Title = draft.Writing.Title };
}
