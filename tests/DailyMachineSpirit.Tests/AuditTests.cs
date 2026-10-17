using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium;
using LanguageExt;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The record (#9), against the Cosmos DB emulator: every change, who made it, saved with the change.</summary>
public sealed class AuditTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    // The Scribe who acted, what they did to which draft, and where it went.
    [Fact]
    public async Task EveryScribeAction_IsRecorded_WithTheScribe()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        var scribes = testbed.Scribes();

        // A minute apart, so newest first is one order.
        Ok(await scribes.Anoint(good.Id, "", Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await scribes.Humble(good.Id, "", Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await scribes.Burn(fair.Id, "", Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await scribes.Restore(fair.Id, "", Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await scribes.LetTheAuguryDecide(Tester, CancellationToken.None));

        var scribesEntries = (await Entries()).Where(entry => entry.Actor == Tester)
            .Select(entry => (entry.Action, entry.Title, entry.Detail.IfNone(string.Empty)));
        Assert.Equal(
        [
            (AuditAction.LetTheAuguryDecide, "", ""),
            (AuditAction.Restore, "Fair", ""),
            (AuditAction.Burn, "Fair", "from #3"),
            (AuditAction.Humble, "Good", "from #1 to #2"),
            (AuditAction.Anoint, "Good", "from #2 to #1"),
        ], scribesEntries);
    }

    // What the Machine Spirit does on its own is recorded too: each draft it writes, and each rite it publishes.
    [Fact]
    public async Task TheMachineSpirit_RecordsTheDraftsItWrites_AndTheRitesItPublishes()
    {
        var draft = await testbed.AddDraft("The Rite of Re-Run", 0.9f);

        var rite = Ok(await testbed.Rites.Publish(draft.Id, Today, CancellationToken.None));

        var entries = await Entries();
        Assert.All(entries, entry => Assert.Equal(AuditEntry.MachineSpirit, entry.Actor));
        Assert.Equal(
            [(AuditAction.Publish, Some(rite.Number)), (AuditAction.Write, Option<int>.None)],
            entries.Select(entry => (entry.Action, entry.RiteNumber)));
        Assert.All(entries, entry => Assert.Equal((Some(draft.Id), "The Rite of Re-Run"), (entry.DraftId, entry.Title)));
    }

    [Fact]
    public async Task AnAmendment_IsRecorded_WithTheTitleItHadBefore()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        var amendable = Ok(await testbed.Amendments().FindWaiting(draft.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));

        Ok(await testbed.Amendments().SaveAndJudgeAgain(draft.Id, amendable.ETag,
            new RiteContent("Litany of the Warm Cache", "O Machine Spirit, warm the cache.", "A warm cache was read before."), "", Tester, CancellationToken.None));

        var entry = (await Entries()).Single(found => found.Action == AuditAction.Amend);
        Assert.Equal((Tester, AuditAction.Amend, "Litany of the Warm Cache", Some("was “Litany of the Cold Cache”")), (entry.Actor, entry.Action, entry.Title, entry.Detail));
    }

    [Fact]
    public async Task OneDraftsRecord_HasItsEntriesAlone()
    {
        var one = await testbed.AddDraft("One", 0.9f);
        await testbed.AddDraft("Another", 0.5f);
        Ok(await testbed.Scribes().Burn(one.Id, "", Tester, CancellationToken.None));

        var record = Ok(await testbed.Audit.GetHistory(one.Id, CancellationToken.None));

        // The Machine Spirit's entries take the real time, the Scribes' the test's clock: which is newer isn't the point.
        Assert.Equal([AuditAction.Burn, AuditAction.Write], record.Select(entry => entry.Action).Order());
    }

    // Cosmos orders the stored strings: a time on the whole second must still sort before one half a second later.
    [Fact]
    public async Task TheRecord_IsNewestFirst_ToTheFractionOfASecond()
    {
        var onTheSecond = new DateTime(2026, 10, 16, 6, 30, 0, DateTimeKind.Utc);
        foreach (var at in new[] { onTheSecond, onTheSecond.AddMilliseconds(500) })
        {
            Ok(await testbed.Audit.Add(
                AuditEntries.Of(testbed.Time, Tester, AuditAction.LetTheAuguryDecide, None, None) with { AtUtc = at },
                CancellationToken.None));
        }

        Assert.Equal([onTheSecond.AddMilliseconds(500), onTheSecond], (await Entries()).Select(entry => entry.AtUtc));
    }

    // However busy a draft's Scribes were, its record goes back to its writing.
    [Fact]
    public async Task OneDraftsRecord_IsWhole_PastTheRecordsLength()
    {
        var draft = await testbed.AddDraft("Much Debated", 0.5f);
        for (var exalted = 0; exalted < ScriptoriumFunction.RecordShown; exalted++)
        {
            Ok(await testbed.Audit.Add(AuditEntries.Of(testbed.Time, Tester, AuditAction.Exalt, Some(draft), None), CancellationToken.None));
        }

        var record = Ok(await testbed.Audit.GetHistory(draft.Id, CancellationToken.None));

        Assert.Equal(ScriptoriumFunction.RecordShown + 1, record.Count);
        Assert.Contains(record, entry => entry.Action == AuditAction.Write);
    }

    // A Scribe who chose a name is shown by it (#84); one who didn't, by the end of their id.
    [Theory]
    [InlineData(AuditEntry.MachineSpirit, "The Machine Spirit")]
    [InlineData(AuditEntry.StagingScribe, "A Scribe on staging")]
    [InlineData("0f7d1c2e-1111-2222-3333-4ab5c6d7e8f9", "A Scribe (…d7e8f9)")]
    [InlineData("1a2b3c4d-0000-0000-0000-000000000000", "Sister Cache")]
    public void TheRecord_NamesEachActor(string actor, string name)
    {
        Assert.Equal(name, ScribeNames.Of(actor, [new ScribeName("1a2b3c4d-0000-0000-0000-000000000000", "Sister Cache")]));
    }

    // Each entry reads as a sentence: who, then what they did to which rite or precept.
    [Theory]
    [InlineData(AuditAction.Anoint, "anointed “Litany”")]
    [InlineData(AuditAction.Exalt, "exalted “Litany”")]
    [InlineData(AuditAction.Humble, "humbled “Litany”")]
    [InlineData(AuditAction.Burn, "consigned “Litany” to the flames")]
    [InlineData(AuditAction.Restore, "restored “Litany” from the ashes")]
    [InlineData(AuditAction.Amend, "amended “Litany”")]
    [InlineData(AuditAction.Summon, "summoned new rites")]
    [InlineData(AuditAction.LetTheAuguryDecide, "let the Augury decide")]
    [InlineData(AuditAction.Write, "wrote “Litany”")]
    [InlineData(AuditAction.Publish, "published “Litany” as NO. 7")]
    [InlineData(AuditAction.Distil, "distilled <a href=\"/scriptorium/precepts\">the Precepts</a>")]
    [InlineData(AuditAction.AddPrecept, "added the precept “Litany”")]
    [InlineData(AuditAction.EditPrecept, "edited the precept “Litany”")]
    [InlineData(AuditAction.PinPrecept, "pinned the precept “Litany”")]
    [InlineData(AuditAction.UnpinPrecept, "unpinned the precept “Litany”")]
    [InlineData(AuditAction.DeletePrecept, "deleted the precept “Litany”")]
    public void TheRecord_SaysWhatEachActionDid(AuditAction action, string did)
    {
        var entry = new AuditEntry { Id = Guid.NewGuid(), Actor = Tester, Action = action, Title = "Litany", RiteNumber = Some(7) };

        Assert.Contains($"A Scribe (…456789) {did}</li>", ScriptoriumPage.RenderRecord([entry], [], None));
    }

    [Fact]
    public void AnEmptyRecord_SaysSo() => Assert.Contains("Nothing is recorded yet.", ScriptoriumPage.RenderRecord([], [], None));

    private async Task<List<AuditEntry>> Entries() => Ok(await testbed.Audit.GetNewest(50, CancellationToken.None));
}
