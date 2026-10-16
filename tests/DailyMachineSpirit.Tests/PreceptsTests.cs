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

        var request = testbed.Models.Requests.Single().Messages.Single(message => message.Role == ChatRole.User).Text;
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
        Ok(await keeping.Add("Keep the Omnissiah out of rituals.", Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled(RealFix, NoRepeats));
        var first = Ok(await testbed.Distiller().Distil(AuditEntry.MachineSpirit, CancellationToken.None));
        Ok(await keeping.Delete(first.Precepts.Single(precept => precept.Text == NoRepeats).Id, Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled("keep the omnissiah out of rituals.", "Name the command in backticks."));

        var second = Ok(await testbed.Distiller().Distil(AuditEntry.MachineSpirit, CancellationToken.None));

        Assert.Equal(["Keep the Omnissiah out of rituals.", "Name the command in backticks."], second.Precepts.Select(precept => precept.Text));
        var request = testbed.Models.Requests.Last().Messages.Single(message => message.Role == ChatRole.User).Text;
        Assert.Contains("Pinned precepts: [\"Keep the Omnissiah out of rituals.\"]", request);
        Assert.Contains($"Rejected precepts: [\"{NoRepeats}\"]", request);
        Assert.Contains("at most 11 PRECEPTS", testbed.Models.Requests.Last().Messages.Single(message => message.Role == ChatRole.System).Text);
    }

    [Fact]
    public async Task Distil_HasNothingToLearn_BeforeAnyJudgment()
    {
        Assert.Equal(PreceptDistiller.NothingToLearn, Failed(await testbed.Distiller().Distil(Tester, CancellationToken.None)));
        Assert.Empty(testbed.Models.Requests);
    }

    [Fact]
    public async Task Distil_LeavesThemAlone_WhenEveryPreceptIsPinned()
    {
        await AJudgment();
        for (var added = 0; added < PreceptBook.MaxPrecepts; added++)
        {
            Ok(await testbed.PreceptKeeping().Add($"Precept {added}.", Tester, CancellationToken.None));
        }

        Assert.Equal(PreceptDistiller.AllPinned, Failed(await testbed.Distiller().Distil(Tester, CancellationToken.None)));
        Assert.Empty(testbed.Models.Requests);
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
        Assert.Equal([Sol, Luna], testbed.Models.Requests.Select(request => request.Model));
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
        Ok(await keeping.Add($"  {RealFix}  ", Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled(NoRepeats));
        var distilled = Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None)).Precepts.Single(precept => !precept.Pinned);

        // A minute apart, so newest first is one order.
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await keeping.Edit(distilled.Id, "Avoid last week's subjects.", Tester, CancellationToken.None));
        var edited = Ok(await keeping.View(CancellationToken.None)).Precepts[1];
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await keeping.Unpin(edited.Id, Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await keeping.Pin(edited.Id, Tester, CancellationToken.None));

        var book = Ok(await keeping.View(CancellationToken.None));
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

    // A deleted precept is remembered, the newest first, so it isn't proposed again; only so many are kept.
    [Fact]
    public async Task Deleting_RemembersTheWords_UpToALimit()
    {
        var keeping = testbed.PreceptKeeping();
        for (var deleted = 0; deleted <= PreceptBook.RejectedKept; deleted++)
        {
            Ok(await keeping.Add($"Precept {deleted}.", Tester, CancellationToken.None));
            var added = Ok(await keeping.View(CancellationToken.None)).Precepts.Single();
            Ok(await keeping.Delete(added.Id, Tester, CancellationToken.None));
        }

        var book = Ok(await keeping.View(CancellationToken.None));
        Assert.Empty(book.Precepts);
        Assert.Equal(PreceptBook.RejectedKept, book.Rejected.Count);
        Assert.Equal($"Precept {PreceptBook.RejectedKept}.", book.Rejected[0]);
    }

    [Theory]
    [InlineData("   ", "A precept needs words.")]
    [InlineData(null, "A precept can be at most 300 characters.")]
    public async Task APrecept_NeedsWords_ButNotTooMany(string? text, string message)
    {
        var error = Failed(await testbed.PreceptKeeping().Add(text ?? new string('x', Precept.MaxLength + 1), Tester, CancellationToken.None));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Adding_StopsAtTheLimit()
    {
        var keeping = testbed.PreceptKeeping();
        for (var added = 0; added < PreceptBook.MaxPrecepts; added++)
        {
            Ok(await keeping.Add($"Precept {added}.", Tester, CancellationToken.None));
        }

        Assert.Equal(PreceptKeeping.Full, Failed(await keeping.Add("One too many.", Tester, CancellationToken.None)));
    }

    // Replaced by a distillation, or deleted by another Scribe, since the page was shown.
    [Fact]
    public async Task APreceptGoneMeanwhile_IsAChangeToLookAt()
        => Assert.Equal(ScriptoriumRepository.ChangedMeanwhile,
            Failed(await testbed.PreceptKeeping().Pin(Guid.NewGuid(), Tester, CancellationToken.None)));

    // Two changes from the same read: the later one must not overwrite the first.
    [Fact]
    public async Task Saving_FromAStaleRead_IsRefused()
    {
        Ok(await testbed.PreceptKeeping().Add(RealFix, Tester, CancellationToken.None));
        var read = Ok(await testbed.Precepts.Get(CancellationToken.None));
        Ok(await testbed.PreceptKeeping().Add(NoRepeats, Tester, CancellationToken.None));

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
        Ok(await testbed.PreceptKeeping().Add(RealFix, Tester, CancellationToken.None));

        var late = await testbed.Precepts.Save(empty, empty.Book,
            new AuditEntry { Id = Guid.NewGuid(), Actor = Tester, Action = AuditAction.AddPrecept }, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(late));
    }

    private async Task AJudgment()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", Tester, CancellationToken.None));
    }
}
