using System.Net;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium;
using LanguageExt;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>A Scribe amends a draft's words (#8), against the Cosmos DB emulator, with fake models and a fake Jev.</summary>
public sealed class AmendmentsTests : IAsyncLifetime
{
    private static readonly RiteContent Sharper = new(
        "The Litany of the Warm Cache",
        "O Machine Spirit, warm thy cache, that `npm ci` may find it full.",
        "`npm ci` deletes `node_modules` first: only a cache outside it survives.");

    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    // The scores describe what will be published, so the new words are judged again; the model that wrote the first
    // version stays known, and the decision keeps the words as they were, for the Augury to learn from.
    [Fact]
    public async Task Save_ReplacesTheWords_MarksThemEdited_KeepsTheModel_AndJudgesThemAgain()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        var amendable = Ok(await testbed.Amendments().Get(draft.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));

        var judged = Ok(await testbed.Amendments().Save(draft.Id, amendable.ETag, Sharper, "The truth was wrong.", BacklogTestbed.Tester, CancellationToken.None));

        Assert.True(judged);
        var saved = Assert.Single(await testbed.Waiting());
        Assert.Equal((Sharper.Title, Sharper.Text, Sharper.HereticalTruth), (saved.Writing.Title, saved.Writing.Text, saved.Writing.HereticalTruth));
        Assert.True(saved.Writing.Edited);
        Assert.Equal(Sol, saved.Writing.GeneratedByModel);
        Assert.Equal(Some(new[] { "builds" }), saved.Tags.Map(tags => tags.ToArray()));
        Assert.True(saved.Similarity.IsSome);
        Assert.NotEqual(Some(0.2f), saved.Augury.Map(augury => augury.Quality));
        Assert.Contains(Sharper.HereticalTruth, testbed.Jev.Requests.Single().Body.ToJsonString().Replace("\\u0060", "`"));
        var decision = Assert.Single(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions);
        Assert.Equal((ScribeAction.Amend, "Litany of the Cold Cache", Some("The truth was wrong.")), (decision.Action, decision.Title, decision.Note));
        Assert.Equal(Some(0.2f), decision.AuguryQuality);
    }

    [Fact]
    public async Task Save_WordsBreakingARule_AreRefusedWithTheReason_AndNothingChanges()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        var amendable = Ok(await testbed.Amendments().Get(draft.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));

        var refused = await testbed.Amendments().Save(draft.Id, amendable.ETag, Sharper with { Text = "Praise the Warhammer of builds." }, "", BacklogTestbed.Tester, CancellationToken.None);

        var error = Assert.Single(refused.LeftToSeq());
        Assert.Equal(Amendments.Refused, error.Code);
        Assert.Contains("forbidden names: Warhammer", error.Message);
        Assert.Equal("Litany of the Cold Cache", Assert.Single(await testbed.Waiting()).Writing.Title);
        Assert.Empty(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions);
    }

    // The version the Scribe read is the one amended: burned, or amended by another Scribe since, it isn't.
    [Fact]
    public async Task Save_AfterAnotherChange_IsRefused_AsChangedMeanwhile()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        var other = await testbed.AddDraft("The Rite of Re-Run", 0.5f);
        var read = Ok(await testbed.Amendments().Get(draft.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));
        var readOther = Ok(await testbed.Amendments().Get(other.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));
        Ok(await testbed.Amendments().Save(draft.Id, read.ETag, Sharper, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Burn(other.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var again = await testbed.Amendments().Save(draft.Id, read.ETag, Sharper with { Title = "Another" }, "", BacklogTestbed.Tester, CancellationToken.None);
        var burned = await testbed.Amendments().Save(other.Id, readOther.ETag, Sharper, "", BacklogTestbed.Tester, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Assert.Single(again.LeftToSeq()));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Assert.Single(burned.LeftToSeq()));
        Assert.Equal(Sharper.Title, Assert.Single(await testbed.Waiting()).Writing.Title);
        Assert.True(Ok(await testbed.Amendments().Get(other.Id, CancellationToken.None)).IsNone);
    }

    // The judge failing doesn't lose the Scribe's words: the draft waits unjudged, like a draft that couldn't be judged.
    [Fact]
    public async Task Save_WhenTheJudgeFails_KeepsTheWords_WithoutTheOldScores()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        testbed.Jev = new FakeJev(HttpStatusCode.InternalServerError, "");
        var amendable = Ok(await testbed.Amendments().Get(draft.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));

        var judged = Ok(await testbed.Amendments().Save(draft.Id, amendable.ETag, Sharper, "", BacklogTestbed.Tester, CancellationToken.None));

        Assert.False(judged);
        var saved = Assert.Single(await testbed.Waiting());
        Assert.Equal(Sharper.Title, saved.Writing.Title);
        Assert.True(saved.Augury.IsNone && saved.Similarity.IsNone && saved.Tags.IsNone);
    }

    [Fact]
    public async Task AnAmendedDraft_StaysEdited_WhenPublished()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        var amendable = Ok(await testbed.Amendments().Get(draft.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));
        Ok(await testbed.Amendments().Save(draft.Id, amendable.ETag, Sharper, "", BacklogTestbed.Tester, CancellationToken.None));

        var rite = Ok(await testbed.Rites.Publish(draft.Id, Today, CancellationToken.None));

        Assert.True((await testbed.PublishedOn(Today)).Writing.Edited);
        Assert.Equal(Sol, rite.Writing.GeneratedByModel);
    }

    // The writer gets the note and the words as they stand; nothing is saved until the Scribe saves the rewrite.
    [Fact]
    public async Task Rewrite_AsksTheWriter_WithTheNote_AndSavesNothing()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        testbed.Models.Answers(Sol, FakeChatClients.Answer(Sharper.Title, Sharper.Text, Sharper.HereticalTruth));

        var rewritten = Ok(await testbed.Amendments().Rewrite(draft.Id, Words(draft), "Name the real fix: `npm ci`.", CancellationToken.None));

        Assert.Equal(Sharper, rewritten);
        var request = Assert.Single(testbed.Models.Requests).Messages.Last().Text;
        // The note goes in as a JSON string, so what a Scribe typed stays words to rewrite by.
        Assert.Contains(JsonSerializer.Serialize("Name the real fix: `npm ci`."), request);
        Assert.Contains("Litany of the Cold Cache", request);
        Assert.Equal("Litany of the Cold Cache", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    [Fact]
    public async Task Rewrite_WithoutANote_IsRefused_AndAsksNoModel()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var refused = await testbed.Amendments().Rewrite(draft.Id, Words(draft), "  ", CancellationToken.None);

        Assert.Equal(Amendments.NoteNeeded, Assert.Single(refused.LeftToSeq()));
        Assert.Empty(testbed.Models.Requests);
    }

    private static RiteContent Words(Draft draft) => new(draft.Writing.Title, draft.Writing.Text, draft.Writing.HereticalTruth);
}
