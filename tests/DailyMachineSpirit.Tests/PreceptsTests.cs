using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Precepts;
using DailyMachineSpirit.Functions.Scriptorium;
using Microsoft.Extensions.AI;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The Precepts (#12), against the Cosmos DB emulator: distilled from the Scribes' judgments, kept by the Scribes.</summary>
public sealed class PreceptsTests : IAsyncLifetime
{
    private const string RealFix = "Prefer rites whose Heretical Truth names the real fix.";
    private const string NoRepeats = "Avoid subjects published in the last two weeks.";

    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    public static string Distilled(params string[] precepts) => JsonSerializer.Serialize(new { precepts });

    /// <summary>The precept check's answer: which precepts, by number, break a rule, and why.</summary>
    public static string Breaking(params (int Number, string Reason)[] breaking)
        => JsonSerializer.Serialize(new { breaking = breaking.Select(broken => new { number = broken.Number, reason = broken.Reason }) });

    // A precept that would have the writer break a rule has no effect: the Scribe is told why at once, and it isn't saved.
    [Fact]
    public async Task AScribesPrecept_BreakingARule_IsRefusedWithWhy()
    {
        var keeping = testbed.PreceptKeeping();
        testbed.Models.Answers(Checker, Breaking((1, "It asks for the setting's names, which the writer never uses.")));

        var refused = Failed(await keeping.AddPinned("Use Adeptus Mechanicus terms.", Tester, CancellationToken.None));

        Assert.Equal(new BreaksARule("It asks for the setting's names, which the writer never uses."), refused);
        Assert.Empty(Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts);
        var request = testbed.Models.Requests.Single(found => found.Model == Checker).Messages;
        Assert.Contains("1. \"Use Adeptus Mechanicus terms.\"", request.Single(message => message.Role == ChatRole.User).Text);
        Assert.Contains("Nothing abusive, hateful, racist, sexist", request.Single(message => message.Role == ChatRole.System).Text);
    }

    [Fact]
    public async Task AnEdit_BreakingARule_IsRefused_AndTheWordsStay()
    {
        var keeping = testbed.PreceptKeeping();
        testbed.Models.Answers(Checker, Breaking());
        Ok(await keeping.AddPinned(RealFix, Tester, CancellationToken.None));
        var id = Ok(await keeping.View(CancellationToken.None)).Book.Precepts.Single().Id;
        testbed.Models.Answers(Checker, Breaking((1, "It mocks a group of people.")));

        var refused = Failed(await keeping.EditAndPin(id, await Version(), "Mock the managers.", Tester, CancellationToken.None));

        Assert.IsType<BreaksARule>(refused);
        Assert.Equal([RealFix], await Texts());
    }

    // Unchecked, a precept isn't saved: the writer would follow it in every rite, and nothing would check it later.
    [Theory]
    [InlineData("""{"breaking": [{"number": 2, "reason": "Out of range."}]}""")]
    [InlineData("""{"breaking": [{"number": 1, "reason": "  "}]}""")]
    [InlineData("not json at all")]
    [InlineData("")]
    public async Task APrecept_NoModelCanCheck_IsRefused(string answer)
    {
        if (answer.Length == 0)
        {
            testbed.Models.Fails(Checker, 1);
        }
        else
        {
            testbed.Models.Answers(Checker, answer);
        }
        testbed.Models.Fails(SecondChecker, 1);

        var refused = Failed(await testbed.PreceptKeeping().AddPinned(RealFix, Tester, CancellationToken.None));

        Assert.Equal(PreceptKeeping.Unchecked, refused);
        Assert.Empty(await Texts());
    }

    // One model failing isn't an outage: the next checks the precepts.
    [Fact]
    public async Task TheCheck_AsksTheNextModel_WhenOneFails()
    {
        testbed.Models.Fails(Checker, 1);
        testbed.Models.Answers(SecondChecker, Breaking((1, "It mocks a group of people.")));

        var refused = Failed(await testbed.PreceptKeeping().AddPinned("Mock the managers.", Tester, CancellationToken.None));

        Assert.Equal(new BreaksARule("It mocks a group of people."), refused);
    }

    // The models share the check's time, so a distillation's wait stays within Cloudflare's: one that takes it all leaves
    // none for the next.
    [Fact]
    public async Task TheCheck_GivesUp_WhenItsTimeIsUsed()
    {
        testbed.Models.Then(Checker, [() =>
        {
            testbed.Time.Advance(Settings.PreceptCheck.Patience.OverallLimit.IfNone(TimeSpan.Zero));
            return new TaskCompletionSource<string>().Task;
        }]);

        var refused = Failed(await testbed.PreceptKeeping().AddPinned(RealFix, Tester, CancellationToken.None));

        Assert.Equal(PreceptKeeping.Unchecked, refused);
        Assert.DoesNotContain(testbed.Models.Requests, request => request.Model == SecondChecker);
    }

    // Distilled precepts that can't be checked aren't kept either: the Precepts stay, and so does what was last read.
    [Fact]
    public async Task Distil_WhenTheCheckCantAnswer_LeavesThePreceptsAsTheyWere()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled(RealFix));
        testbed.Models.Fails(Checker, 1);
        testbed.Models.Fails(SecondChecker, 1);

        var refused = Failed(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        Assert.Equal(PreceptDistiller.Unchecked, refused);
        var book = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book;
        Assert.Empty(book.Precepts);
        Assert.True(book.NewestJudgmentReadUtc.IsNone);
    }

    // When every distilled precept breaks a rule, nothing replaces the unpinned ones: they stay.
    [Fact]
    public async Task Distil_WithEveryPreceptBreakingARule_LeavesThePreceptsAsTheyWere()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled(RealFix));
        Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));
        var before = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book;
        testbed.Time.Advance(TimeSpan.FromDays(7));
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled("Quote the Litany of Ignition word for word."));
        testbed.Models.Answers(Checker, Breaking((1, "It quotes the setting's text.")));

        var refused = Failed(await testbed.Distiller().DistilIfJudgedSinceLast(CancellationToken.None));

        Assert.Equal(PreceptDistiller.NothingWithinTheRules, refused);
        var after = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book;
        Assert.Equal([RealFix], after.Precepts.Select(precept => precept.Text));
        // What was last read stays, so next week's run tries these judgments again.
        Assert.Equal((before.DistilledAtUtc, before.NewestJudgmentReadUtc), (after.DistilledAtUtc, after.NewestJudgmentReadUtc));
    }

    // Distilled, a precept breaking a rule is dropped: followed, it would fail drafts or be ignored.
    [Fact]
    public async Task Distil_DropsPreceptsThatBreakARule()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled(RealFix, "Quote the Litany of Ignition word for word."));
        testbed.Models.Answers(Checker, Breaking((2, "It quotes the setting's text.")));

        Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        Assert.Equal([RealFix], await Texts());
        Assert.Contains("2. \"Quote the Litany of Ignition word for word.\"",
            testbed.Models.Requests.Single(found => found.Model == Checker).Messages.Single(message => message.Role == ChatRole.User).Text);
    }

    // The writer and the distillation read the same rules the check does.
    [Fact]
    public void TheWriterAndTheDistillation_AreToldTheRules()
    {
        var abuse = "Nothing abusive, hateful, racist, sexist or otherwise discriminating";
        Assert.Contains(abuse, PreceptPrompt.Instructions(3));
        Assert.Contains("Never propose a precept that would have the writer break one of its rules", PreceptPrompt.Instructions(3));
        Assert.Contains(abuse, DailyMachineSpirit.Functions.Generation.Writing.RitePrompt.Instructions(RiteKind.Ritual, [], []));
        Assert.Contains("never in a title, a Heretical Truth, an ailment or a ritual", PreceptPrompt.Instructions(3));
    }

    // The judgments and their notes go to the model; what it answers becomes the Precepts, recorded as who asked.
    [Fact]
    public async Task Distil_LearnsFromTheJudgments_AndRecordsWhoAsked()
    {
        var draft = await testbed.AddDraft("The Rite of Re-Run", 0.9f);
        Ok(await testbed.Scribes().Burn(draft.Id, "The truth says nothing about the race.", Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled($"  {RealFix} ", NoRepeats, ""));

        var book = Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        Assert.Equal([RealFix, NoRepeats], book.Precepts.Select(precept => precept.Text));
        Assert.All(book.Precepts, precept => Assert.False(precept.Pinned || precept.ByScribe));
        Assert.Equal((Some(testbed.Time.GetUtcNow().UtcDateTime), Some(1)), (book.DistilledAtUtc, book.DistilledFrom));
        Assert.Equal([RealFix, NoRepeats], Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Select(precept => precept.Text));

        var request = Distilling().Single().Messages.Single(message => message.Role == ChatRole.User).Text;
        Assert.Contains("The truth says nothing about the race.", request);
        Assert.Contains("\"action\":\"burn\"", request);
        var entry = Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)).Single(found => found.Action == AuditAction.Distil);
        Assert.Equal((Tester, AuditAction.Distil, Some("2 new, 0 pinned, from 1 judgments")), (entry.Actor, entry.Action, entry.Detail));
    }

    // Pinned precepts stay, first; the unpinned make way. The model hears what's pinned and what was deleted, and a
    // repeat of a pinned one is dropped.
    [Fact]
    public async Task Distil_KeepsThePinned_AndReplacesTheRest()
    {
        await AJudgment();
        var keeping = testbed.PreceptKeeping();
        Ok(await keeping.AddPinned("Keep the Omnissiah out of rituals.", Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled(RealFix, NoRepeats));
        var first = Ok(await testbed.Distiller().Distil(AuditEntry.MachineSpirit, CancellationToken.None));
        Ok(await keeping.Delete(first.Precepts.Single(precept => precept.Text == NoRepeats).Id, await Version(), Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled("keep the omnissiah out of rituals.", "Name the command in backticks."));

        var second = Ok(await testbed.Distiller().Distil(AuditEntry.MachineSpirit, CancellationToken.None));

        Assert.Equal(["Keep the Omnissiah out of rituals.", "Name the command in backticks."], second.Precepts.Select(precept => precept.Text));
        var request = Distilling().Last().Messages.Single(message => message.Role == ChatRole.User).Text;
        Assert.Contains("Pinned precepts: [\"Keep the Omnissiah out of rituals.\"]", request);
        Assert.Contains($"Rejected precepts: [\"{NoRepeats}\"]", request);
        Assert.Contains("at most 11 PRECEPTS", Distilling().Last().Messages.Single(message => message.Role == ChatRole.System).Text);
    }

    [Fact]
    public async Task Distil_HasNothingToLearn_BeforeAnyJudgment()
    {
        Assert.Equal(PreceptDistiller.NothingToLearn, Failed(await testbed.Distiller().Distil(Tester, CancellationToken.None)));
        Assert.Empty(Distilling());
    }

    [Fact]
    public async Task Distil_LeavesThemAlone_WhenEveryPreceptIsPinned()
    {
        await AJudgment();
        for (var added = 0; added < PreceptBook.MaxPrecepts; added++)
        {
            Ok(await testbed.PreceptKeeping().AddPinned($"Precept {added}.", Tester, CancellationToken.None));
        }

        Assert.Equal(PreceptDistiller.AllPinned, Failed(await testbed.Distiller().Distil(Tester, CancellationToken.None)));
        Assert.Empty(Distilling());
    }

    // An answer that breaks the rules isn't trusted: the next model is asked, and only room enough is taken.
    [Fact]
    public async Task Distil_AsksTheNextModel_WhenAnAnswerIsUnusable()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled(new string('x', Precept.MaxLength + 1)));
        testbed.Models.Answers(Luna, Distilled([.. Enumerable.Range(0, 20).Select(index => $"Precept {index}.")]));

        var book = Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        Assert.Equal(PreceptBook.MaxPrecepts, book.Precepts.Count);
        Assert.Equal([Sol, Luna], Distilling().Select(request => request.Model));
    }

    // The serializer checks the list, not what's in it.
    [Fact]
    public async Task Distil_AsksTheNextModel_WhenAPreceptIsNull()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, """{"precepts":[null]}""");
        testbed.Models.Answers(Luna, Distilled(RealFix));

        var book = Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        Assert.Equal([RealFix], book.Precepts.Select(precept => precept.Text));
    }

    // Deleting promised it won't come back: an answer of nothing but deleted precepts goes to the next model.
    [Fact]
    public async Task Distil_NeverBringsBackADeletedPrecept()
    {
        await AJudgment();
        Ok(await testbed.PreceptKeeping().AddPinned(NoRepeats, Tester, CancellationToken.None));
        var read = Ok(await testbed.Precepts.Get(CancellationToken.None));
        Ok(await testbed.PreceptKeeping().Delete(read.Book.Precepts.Single().Id, ETag(read), Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled(NoRepeats.ToUpperInvariant()));
        testbed.Models.Answers(Luna, Distilled(NoRepeats, RealFix));

        var book = Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        Assert.Equal([RealFix], book.Precepts.Select(precept => precept.Text));
    }

    // A model that doesn't answer in time gives way to the next, so a Scribe isn't kept waiting.
    [Fact]
    public async Task Distil_AsksTheNextModel_WhenOneTakesTooLong()
    {
        await AJudgment();
        testbed.Models.Then(Sol, [() =>
        {
            testbed.Time.Advance(Settings.PreceptDistilling.Patience.TryLimit.IfNone(TimeSpan.Zero));
            return new TaskCompletionSource<string>().Task;
        }]);
        testbed.Models.Answers(Luna, Distilled(RealFix));

        var book = Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        Assert.Equal([RealFix], book.Precepts.Select(precept => precept.Text));
    }

    [Fact]
    public async Task Distil_SavesNothing_WhenNoModelAnswersUsably()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled("", "  "));
        testbed.Models.Fails(Luna, 1);

        var error = Failed(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        Assert.Contains("No model distilled usable precepts.", error.Message);
        Assert.Empty(Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts);
    }

    // The Scribes' own words are pinned, so distilling again keeps them; each change is recorded.
    [Fact]
    public async Task TheScribes_Add_Edit_Pin_AndUnpin_Precepts()
    {
        var keeping = testbed.PreceptKeeping();
        Ok(await keeping.AddPinned($"  {RealFix}  ", Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled(NoRepeats));
        var distilled = Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None)).Precepts.Single(precept => !precept.Pinned);

        // A minute apart, so newest first is one order.
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await keeping.EditAndPin(distilled.Id, await Version(), "Avoid last week's subjects.", Tester, CancellationToken.None));
        var edited = Ok(await keeping.View(CancellationToken.None)).Book.Precepts[1];
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await keeping.Unpin(edited.Id, await Version(), Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await keeping.Pin(edited.Id, await Version(), Tester, CancellationToken.None));

        var book = Ok(await keeping.View(CancellationToken.None)).Book;
        Assert.Equal([(RealFix, true, true), ("Avoid last week's subjects.", true, true)],
            book.Precepts.Select(precept => (precept.Text, precept.Pinned, precept.ByScribe)));
        var record = Ok(await testbed.Audit.GetNewest(10, CancellationToken.None))
            .Where(entry => entry.Action is not (AuditAction.Burn or AuditAction.Write))
            .Select(entry => (entry.Action, entry.Title, entry.Detail.IfNone(string.Empty)));
        Assert.Equal(
        [
            (AuditAction.PinPrecept, "Avoid last week's subjects.", ""),
            (AuditAction.UnpinPrecept, "Avoid last week's subjects.", ""),
            (AuditAction.EditPrecept, "Avoid last week's subjects.", $"was “{NoRepeats}”"),
            (AuditAction.Distil, "", "1 new, 1 pinned, from 1 judgments"),
            (AuditAction.AddPrecept, RealFix, ""),
        ], record);
    }

    // The same judgments would only reword the unpinned precepts: the weekly run waits for a new one, while a Scribe who
    // asks always gets a distillation.
    [Fact]
    public async Task TheWeeklyDistillation_WaitsForANewJudgment()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled(RealFix), Distilled(NoRepeats), Distilled("Prefer short rituals."));
        Ok(await testbed.Distiller().DistilIfJudgedSinceLast(CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromDays(7));

        Assert.Equal(PreceptDistiller.NothingNewToLearn, Failed(await testbed.Distiller().DistilIfJudgedSinceLast(CancellationToken.None)));
        Assert.Equal([RealFix], await Texts());

        await AJudgment();
        testbed.Time.Advance(TimeSpan.FromDays(7));
        Ok(await testbed.Distiller().DistilIfJudgedSinceLast(CancellationToken.None));
        Assert.Equal([NoRepeats], await Texts());

        Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));
        Assert.Equal(["Prefer short rituals."], await Texts());
    }

    // A judgment made while the model answered isn't in its answer, though it's older than the distillation: the next
    // weekly run learns from it.
    [Fact]
    public async Task AJudgmentMadeWhileDistilling_IsLearnedTheNextWeek()
    {
        await AJudgment();
        testbed.Models.Then(Sol,
        [
            async () =>
            {
                testbed.Time.Advance(TimeSpan.FromSeconds(10));
                await AJudgment();
                testbed.Time.Advance(TimeSpan.FromSeconds(10));
                return Distilled(RealFix);
            },
        ]);
        Ok(await testbed.Distiller().DistilIfJudgedSinceLast(CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromDays(7));
        testbed.Models.Answers(Sol, Distilled(NoRepeats));

        Ok(await testbed.Distiller().DistilIfJudgedSinceLast(CancellationToken.None));

        Assert.Equal([NoRepeats], await Texts());
    }

    // The pinned lead, on the page and in the writer's prompt; each group keeps its order.
    [Fact]
    public async Task ThePinned_ComeFirst()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled("First.", "Second.", "Third."));
        Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));
        var keeping = testbed.PreceptKeeping();

        Ok(await keeping.AddPinned("Mine.", Tester, CancellationToken.None));
        Assert.Equal(["Mine.", "First.", "Second.", "Third."], await Texts());
        Ok(await keeping.Pin(await IdOf("Third."), await Version(), Tester, CancellationToken.None));
        Assert.Equal(["Mine.", "Third.", "First.", "Second."], await Texts());
        Ok(await keeping.Unpin(await IdOf("Mine."), await Version(), Tester, CancellationToken.None));
        Assert.Equal(["Third.", "Mine.", "First.", "Second."], await Texts());
    }

    // A deleted precept is remembered, the newest first, so it isn't proposed again; only so many are kept.
    [Fact]
    public async Task Deleting_RemembersTheWords_UpToALimit()
    {
        var keeping = testbed.PreceptKeeping();
        for (var deleted = 0; deleted <= PreceptBook.RejectedKept; deleted++)
        {
            Ok(await keeping.AddPinned($"Precept {deleted}.", Tester, CancellationToken.None));
            var added = Ok(await keeping.View(CancellationToken.None)).Book.Precepts.Single();
            Ok(await keeping.Delete(added.Id, await Version(), Tester, CancellationToken.None));
        }

        var book = Ok(await keeping.View(CancellationToken.None)).Book;
        Assert.Empty(book.Precepts);
        Assert.Equal(PreceptBook.RejectedKept, book.Rejected.Count);
        Assert.Equal($"Precept {PreceptBook.RejectedKept}.", book.Rejected[0]);
    }

    [Theory]
    [InlineData("   ", "A precept needs words.")]
    [InlineData(null, "A precept can be at most 300 characters.")]
    public async Task APrecept_NeedsWords_ButNotTooMany(string? text, string message)
    {
        var error = Failed(await testbed.PreceptKeeping().AddPinned(text ?? new string('x', Precept.MaxLength + 1), Tester, CancellationToken.None));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Adding_StopsAtTheLimit()
    {
        var keeping = testbed.PreceptKeeping();
        for (var added = 0; added < PreceptBook.MaxPrecepts; added++)
        {
            Ok(await keeping.AddPinned($"Precept {added}.", Tester, CancellationToken.None));
        }

        Assert.Equal(PreceptKeeping.Full, Failed(await keeping.AddPinned("One too many.", Tester, CancellationToken.None)));
    }

    // Replaced by a distillation, or deleted by another Scribe, since the page was shown.
    [Fact]
    public async Task APreceptGoneMeanwhile_IsAChangeToLookAt()
        => Assert.Equal(ScriptoriumRepository.ChangedMeanwhile,
            Failed(await testbed.PreceptKeeping().Pin(Guid.NewGuid(), await Version(), Tester, CancellationToken.None)));

    // Two changes from the same read: the later one must not overwrite the first.
    [Fact]
    public async Task Saving_FromAStaleRead_IsRefused()
    {
        Ok(await testbed.PreceptKeeping().AddPinned(RealFix, Tester, CancellationToken.None));
        var read = Ok(await testbed.Precepts.Get(CancellationToken.None));
        Ok(await testbed.PreceptKeeping().AddPinned(NoRepeats, Tester, CancellationToken.None));

        var stale = await testbed.Precepts.Save(read, read.Book with { Precepts = [] },
            new AuditEntry { Id = Guid.NewGuid(), Actor = Tester, Action = AuditAction.DeletePrecept }, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(stale));
        Assert.Equal(2, Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Count);
    }

    // The first save creates the document: two at once can't both create it.
    [Fact]
    public async Task TheFirstSave_IsRefused_WhenAnotherCameFirst()
    {
        var empty = Ok(await testbed.Precepts.Get(CancellationToken.None));
        Ok(await testbed.PreceptKeeping().AddPinned(RealFix, Tester, CancellationToken.None));

        var late = await testbed.Precepts.Save(empty, empty.Book,
            new AuditEntry { Id = Guid.NewGuid(), Actor = Tester, Action = AuditAction.AddPrecept }, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(late));
    }

    private async Task<List<string>> Texts()
        => Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Select(precept => precept.Text).ToList();

    private async Task<Guid> IdOf(string text)
        => Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single(precept => precept.Text == text).Id;

    // The distilling models' requests, without the precept check's.
    private List<(string Model, List<ChatMessage> Messages)> Distilling()
        => testbed.Models.Requests.Where(request => request.Model != Checker).ToList();

    // The version of the Precepts a Scribe's page would show now.
    private async Task<string> Version() => ETag(Ok(await testbed.Precepts.Get(CancellationToken.None)));

    private static string ETag(StoredPrecepts read) => read.ETag.IfNone(string.Empty);

    private async Task AJudgment()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", Tester, CancellationToken.None));
    }
}
