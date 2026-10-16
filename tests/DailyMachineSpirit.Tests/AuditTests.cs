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
        var amendable = Ok(await testbed.Amendments().Get(draft.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));

        Ok(await testbed.Amendments().Save(draft.Id, amendable.ETag,
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

        var record = Ok(await testbed.Audit.GetNewest(10, Some(one.Id), CancellationToken.None));

        // The Machine Spirit's entries take the real time, the Scribes' the test's clock: which is newer isn't the point.
        Assert.Equal([AuditAction.Burn, AuditAction.Write], record.Select(entry => entry.Action).Order());
    }

    [Theory]
    [InlineData(AuditEntry.MachineSpirit, "The Machine Spirit")]
    [InlineData(AuditEntry.StagingScribe, "A Scribe on staging")]
    [InlineData("7335d417-61da-459d-899c-0a1f8f6f0d3b", "Mykola")]
    [InlineData("0f7d1c2e-1111-2222-3333-4ab5c6d7e8f9", "A Scribe (…d7e8f9)")]
    public void TheRecord_NamesEachActor(string actor, string name)
        => Assert.Equal(name, ScribeNames.Of(actor, new Dictionary<string, string> { ["7335d417-61da-459d-899c-0a1f8f6f0d3b"] = "Mykola" }));

    private async Task<List<AuditEntry>> Entries() => Ok(await testbed.Audit.GetNewest(50, None, CancellationToken.None));
}
