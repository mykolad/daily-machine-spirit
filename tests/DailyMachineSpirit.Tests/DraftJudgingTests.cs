using System.Net;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Judging;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Judging the drafts the current judge hasn't judged, against the Cosmos DB emulator with a fake Jev.</summary>
public sealed class DraftJudgingTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    // An outage that's over heals: each refill judges the unjudged before it writes, so they get their scores, quality
    // and tags, recorded as the Machine Spirit's. The judged are left as they are.
    [Fact]
    public async Task ARefill_JudgesTheUnjudgedFirst_AndRecordsIt()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        var judged = await testbed.AddDraft("Already judged", 0.9f);
        testbed.BacklogSize = 2;

        Assert.Equal(0, Ok(await testbed.Refiller().Refill(CancellationToken.None)));

        var saved = Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Gone."));
        Assert.True(saved.Augury.IsSome && saved.Similarity.IsSome && saved.Tags.IsSome);
        Assert.Equal(Some(0.9f), Ok(await testbed.Drafts.Get(judged.Id, CancellationToken.None)).Bind(draft => draft.Augury).Map(augury => augury.Quality));
        var entry = Assert.Single(Ok(await testbed.Audit.GetHistory(unjudged.Id, CancellationToken.None)), found => found.Action == AuditAction.JudgeAgain);
        Assert.Equal(AuditEntry.MachineSpirit, entry.Actor);
        Assert.StartsWith("the Augury: ", entry.Detail.IfNone(string.Empty));
        Assert.DoesNotContain(Ok(await testbed.Audit.GetHistory(judged.Id, CancellationToken.None)), found => found.Action == AuditAction.JudgeAgain);
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
            testbed.QualityJudgesFail(Settings.QualityJudging.ScoresPerDraft);
        }

        Assert.True(Ok(await testbed.DraftJudging().Judge(unjudged.Id, Tester, CancellationToken.None)).IsNone);

        Assert.True(Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).Bind(draft => draft.Augury).IsNone);
        Assert.DoesNotContain(Ok(await testbed.Audit.GetHistory(unjudged.Id, CancellationToken.None)), entry => entry.Action == AuditAction.JudgeAgain);
    }

    [Fact]
    public async Task AJudgedDraft_HasNothingToJudge()
    {
        var judged = await testbed.AddDraft("Already judged", 0.9f);

        Assert.Equal(DraftJudging.NothingToJudge, Failed(await testbed.DraftJudging().Judge(judged.Id, Tester, CancellationToken.None)));
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

    // A quality from an older judge (Jev's question, before #10) can't be compared with the current judge's: the draft
    // would wait behind every new one. The next refill judges it again.
    [Fact]
    public async Task ADraftAnOlderJudgeJudged_IsJudgedByTheCurrentOne()
    {
        var older = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        Ok(await testbed.Drafts.SaveJudgmentIfUnchanged(
            older with { Augury = new Augury { Quality = 0.9f, JudgedBy = "jev-1.13.0/quality-q1" } }, await ETag(older), Entry(older), CancellationToken.None));
        testbed.BacklogSize = 1;

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var judged = Ok(await testbed.Drafts.Get(older.Id, CancellationToken.None)).Bind(draft => draft.Augury);
        Assert.Equal(Some(RubricJudge.Version), judged.Map(augury => augury.JudgedBy));
        Assert.Equal(Some(7f / 9), judged.Map(augury => augury.Quality));
    }

    // Scores from another Jev model can't be compared with the current one's, so "More rites" and the Augury would treat
    // the draft as like nothing else. It's judged again for new ones.
    [Fact]
    public async Task ADraftAnOlderJevScored_IsJudgedAgain()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.9f);
        var older = draft.Similarity.Map(scores => scores with { ScoresGeneratorVersion = JevQuestions.Version("jev-1.12.0") });
        Ok(await testbed.Drafts.SaveJudgmentIfUnchanged(draft with { Similarity = older }, await ETag(draft), Entry(draft), CancellationToken.None));

        Assert.True(Ok(await testbed.DraftJudging().Judge(draft.Id, AuditEntry.MachineSpirit, CancellationToken.None)).IsSome);

        var judged = Ok(await testbed.Drafts.Get(draft.Id, CancellationToken.None)).Bind(found => found.Similarity);
        Assert.Equal(Some(JevQuestions.Version(Settings.Jev.Model)), judged.Map(scores => scores.ScoresGeneratorVersion));
    }

    // Judged while Jev was off, a draft has its quality but no scores or tags. Once Jev is on again, it's judged again
    // for them, so it isn't published without them.
    [Fact]
    public async Task ADraftJudgedWhileJevWasOff_IsJudgedAgain_OnceJevIsOn()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        testbed.JevApiKey = "";
        Assert.True(Ok(await testbed.DraftJudging().Judge(unjudged.Id, AuditEntry.MachineSpirit, CancellationToken.None)).IsSome);
        Assert.True(Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).Bind(draft => draft.Similarity).IsNone);

        testbed.JevApiKey = FakeJev.ApiKey;
        Assert.True(Ok(await testbed.DraftJudging().Judge(unjudged.Id, AuditEntry.MachineSpirit, CancellationToken.None)).IsSome);

        var judged = Ok(await testbed.Drafts.Get(unjudged.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Gone."));
        Assert.True(judged.Similarity.IsSome);
        Assert.Equal(Some(new[] { "builds" }), judged.Tags);
        Assert.Equal(DraftJudging.NothingToJudge, Failed(await testbed.DraftJudging().Judge(unjudged.Id, AuditEntry.MachineSpirit, CancellationToken.None)));
    }

    private async Task<string> ETag(Draft draft)
        => Ok(await testbed.Drafts.GetWithETag(draft.Id, CancellationToken.None)).Map(found => found.ETag)
            .IfNone(() => throw new Xunit.Sdk.XunitException("Gone."));

    private static AuditEntry Entry(Draft draft)
        => new() { Id = Guid.NewGuid(), Actor = Tester, Action = AuditAction.JudgeAgain, DraftId = draft.Id, Title = draft.Writing.Title };
}
