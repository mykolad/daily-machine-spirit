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

    // Jev still failing, or the quality's models: nothing is saved, and nothing recorded.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhileJudgingFails_NothingChanges(bool jevFailing)
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        if (jevFailing)
        {
            testbed.Jev = new FakeJev(HttpStatusCode.InternalServerError, "");
        }
        else
        {
            // Each judging asks for its scores at once: two judgings.
            testbed.QualityJudgesFail(2 * Settings.QualityJudging.ScoresPerDraft);
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
        var read = await ETag(unjudged);
        var first = unjudged with { Augury = new Augury { Quality = 0.8f, JudgedBy = "first" } };
        var second = unjudged with { Augury = new Augury { Quality = 0.2f, JudgedBy = "second" } };
        Ok(await testbed.Drafts.SaveJudgmentIfUnchanged(first, read, Entry(unjudged), CancellationToken.None));

        var late = await testbed.Drafts.SaveJudgmentIfUnchanged(second, read, Entry(unjudged), CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(late));
        Assert.Equal(Some("first"), Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).Bind(draft => draft.Augury).Map(augury => augury.JudgedBy));
    }

    // Amended meanwhile, and the amendment couldn't be judged either: it's still unjudged, but a judgment of the old words
    // mustn't land on the new ones.
    [Fact]
    public async Task AJudgmentOfOldWords_DoesntLandOnAnAmendmentSavedMeanwhile()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        var read = await ETag(unjudged);
        testbed.QualityJudgesFail(Settings.QualityJudging.ScoresPerDraft);
        var amendable = Ok(await testbed.Amendments().FindWaiting(unjudged.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));
        Ok(await testbed.Amendments().SaveAndJudgeAgain(unjudged.Id, amendable.ETag,
            new("Litany of the Warm Cache", "O Machine Spirit, warm thy cache.", "A warm cache was read before."), "", Tester, CancellationToken.None));

        var stale = await testbed.Drafts.SaveJudgmentIfUnchanged(
            unjudged with { Augury = new Augury { Quality = 0.8f, JudgedBy = "old words" } }, read, Entry(unjudged), CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(stale));
        var saved = Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Gone."));
        Assert.Equal(("Litany of the Warm Cache", true), (saved.Writing.Title, saved.Augury.IsNone));
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

    // A quality from an older judge (Jev's question, before #10) can't be compared with the current judge's: the draft
    // would wait behind every new one. The next refill judges it again.
    [Fact]
    public async Task ADraftAnOlderJudgeJudged_IsJudgedAgain()
    {
        var older = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        Ok(await testbed.Drafts.SaveJudgmentIfUnchanged(
            older with { Augury = new Augury { Quality = 0.9f, JudgedBy = "jev-1.13.0/quality-q1" } }, await ETag(older), Entry(older), CancellationToken.None));

        Assert.Equal(1, Ok(await testbed.JudgingAgain().JudgeTheUnjudged(CancellationToken.None)));

        var judged = Ok(await testbed.Drafts.Get(older.Id, CancellationToken.None)).Bind(draft => draft.Augury);
        Assert.Equal(Some(RubricJudge.Version), judged.Map(augury => augury.JudgedBy));
        Assert.Equal(Some(7f / 9), judged.Map(augury => augury.Quality));
    }

    // Judged while Jev was off, a draft has its quality but no scores or tags. Once Jev is on again, it's judged again
    // for them, so it isn't published without them.
    [Fact]
    public async Task ADraftJudgedWhileJevWasOff_IsJudgedAgain_OnceJevIsOn()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        testbed.JevApiKey = "";
        Assert.Equal(1, Ok(await testbed.JudgingAgain().JudgeTheUnjudged(CancellationToken.None)));
        Assert.True(Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).Bind(draft => draft.Similarity).IsNone);

        testbed.JevApiKey = FakeJev.ApiKey;
        Assert.Equal(1, Ok(await testbed.JudgingAgain().JudgeTheUnjudged(CancellationToken.None)));

        var judged = Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Gone."));
        Assert.True(judged.Similarity.IsSome);
        Assert.Equal(Some(new[] { "builds" }), judged.Tags);
        Assert.Equal(0, Ok(await testbed.JudgingAgain().JudgeTheUnjudged(CancellationToken.None)));
    }

    private async Task<string> ETag(Draft draft)
        => Ok(await testbed.Drafts.GetWithETag(draft.Id, CancellationToken.None)).Map(found => found.ETag)
            .IfNone(() => throw new Xunit.Sdk.XunitException("Gone."));

    private static AuditEntry Entry(Draft draft)
        => new() { Id = Guid.NewGuid(), Actor = Tester, Action = AuditAction.JudgeAgain, DraftId = draft.Id, Title = draft.Writing.Title };
}
