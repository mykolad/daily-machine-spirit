using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Giving the drafts burned before a draft kept its burn's details the Record's, in the Cosmos DB emulator.</summary>
public sealed class BurnBackfillTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task Starting_GivesOlderBurnsTheRecordsDetails_AndAfterwardsNothingIsLeft()
    {
        await testbed.AddDraft("Excellent", 0.9f);
        var burned = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Burn(burned.Id, "Too specific", BacklogTestbed.Tester, CancellationToken.None));
        await testbed.ForgetBurnDetails(burned.Id);

        Assert.Equal(1, Ok(await Backfill().Run(CancellationToken.None)));

        var draft = Ok(await testbed.Drafts.Get(burned.Id, CancellationToken.None)).IfNone(() => throw new InvalidOperationException("gone"));
        Assert.Equal(Some(BacklogTestbed.Tester), draft.BurnedBy);
        Assert.Equal(Some(2), draft.BurnedFromPlace);
        Assert.Equal(Some("Too specific"), draft.BurnReason);
        Assert.Equal(0, Ok(await Backfill().Run(CancellationToken.None)));
    }

    // Older than the Record, a burn has nothing to say; it's marked as looked at, so the next start doesn't look again.
    [Fact]
    public async Task ABurnTheRecordDoesntKnow_StaysWithoutDetails_AndIsntLookedForAgain()
    {
        var waiting = await testbed.AddDraft("Burned long ago", 0.5f);
        var ancient = Ok(await testbed.Drafts.Add(waiting with { Id = Guid.NewGuid(), State = DraftState.Burned }, CancellationToken.None));
        await testbed.ForgetBurnDetails(ancient.Id);

        Assert.Equal(1, Ok(await Backfill().Run(CancellationToken.None)));

        Assert.Empty(Ok(await testbed.Burns.GetBurnedWithoutDetails(CancellationToken.None)));
        var kept = Ok(await testbed.Drafts.Get(ancient.Id, CancellationToken.None)).IfNone(() => throw new InvalidOperationException("gone"));
        Assert.True(kept.BurnedBy.IsNone && kept.BurnedFromPlace.IsNone && kept.BurnReason.IsNone);
    }

    // Restored meanwhile, the draft keeps no burn: the Record's older one mustn't be written onto it.
    [Fact]
    public async Task ADraftRestoredMeanwhile_IsLeftAsItIs()
    {
        var draft = await testbed.AddDraft("Back again", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "Too specific", BacklogTestbed.Tester, CancellationToken.None));
        await testbed.ForgetBurnDetails(draft.Id);
        var burn = Ok(await testbed.Burns.FindInTheRecord(draft.Id, CancellationToken.None));
        Ok(await testbed.Scribes().Restore(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        Ok(await testbed.Burns.SaveDetails(draft.Id, burn, CancellationToken.None));

        var waiting = Ok(await testbed.Drafts.Get(draft.Id, CancellationToken.None)).IfNone(() => throw new InvalidOperationException("gone"));
        Assert.True(waiting.BurnedBy.IsNone && waiting.BurnReason.IsNone);
    }

    private BurnBackfill Backfill() => new(testbed.Burns, NullLogger<BurnBackfill>.Instance);
}
