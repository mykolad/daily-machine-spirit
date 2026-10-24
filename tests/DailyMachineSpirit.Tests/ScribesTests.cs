using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Scriptorium;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>What the Scribes do, against the Cosmos DB emulator: the calendar, their order, burning, and their notes.</summary>
public sealed class ScribesTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task View_ShowsTheCalendarInTheAuguryOrder_WithTheDaysTheyGoOut()
    {
        await testbed.AddDraft("Fair", 0.4f);
        await testbed.AddDraft("Excellent", 0.95f, RiteKind.Ritual);

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));

        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        // Today's rite isn't out yet, so the first goes out tonight... which is today's.
        Assert.Equal([Today, Today.AddDays(1)], view.Calendar.Select(entry => entry.PublishesOnUtc));
        Assert.Equal([1, 2], view.Calendar.Select(entry => entry.AuguryPlace));
        Assert.All(view.Calendar, entry => Assert.False(entry.PlacedByScribes));
        Assert.Empty(Ok(await testbed.Scribes().Ashes(CancellationToken.None)));
        Assert.Empty(view.Decisions);
    }

    [Fact]
    public async Task View_AfterTodaysRite_StartsTheCalendarTomorrow()
    {
        await testbed.AddRite(Today, "Today's");
        await testbed.AddDraft("Next", 0.5f);

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));

        Assert.Equal(Today.AddDays(1), Assert.Single(view.Calendar).PublishesOnUtc);
    }

    // Each anointed draft is the Scribe's who anointed it, or who last moved it; a move leaves the other's Scribe as it
    // was, and the Augury's drafts have none.
    [Fact]
    public async Task TheAnointed_KeepWhoAnointedOrMovedThem()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);

        Ok(await testbed.Scribes().Anoint(good.Id, "", new ScribeId("first-scribe"), CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(excellent.Id, "", new ScribeId("second-scribe"), CancellationToken.None));
        Ok(await testbed.Scribes().Humble(good.Id, "", new ScribeId("third-scribe"), CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Good", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal([Some(new ScribeId("second-scribe")), Some(new ScribeId("third-scribe")), None], view.Calendar.Select(entry => entry.PlacedBy));
    }

    // A draft burned or published leaves the order, and who placed it with it; the Augury's drafts have no placer.
    [Fact]
    public async Task WhoPlacedADraft_LeavesTheOrderWithIt()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(excellent.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        Ok(await testbed.Scribes().Burn(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var placements = Ok(await testbed.Scriptorium.GetPlacements(CancellationToken.None));
        Assert.DoesNotContain(good.Id, placements.PlacedBy.Keys);
        Assert.Equal(placements.Order.Order(), placements.PlacedBy.Keys.Order());
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal([Some(BacklogTestbed.Tester), None], view.Calendar.Select(entry => entry.PlacedBy));
    }

    // Publishing leaves the order as it was, as it always has: the published draft no longer waits, so the calendar
    // doesn't show it, and the next change to the order leaves it out, with who placed it.
    [Fact]
    public async Task WhoPlacedAPublishedDraft_LeavesWithTheNextChangeToTheOrder()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(excellent.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Ok(await testbed.Scribes().Anoint(fair.Id, "", new ScribeId("another-scribe"), CancellationToken.None));

        var placements = Ok(await testbed.Scriptorium.GetPlacements(CancellationToken.None));
        Assert.DoesNotContain(good.Id, placements.Order);
        Assert.DoesNotContain(good.Id, placements.PlacedBy.Keys);
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal([Some(BacklogTestbed.Tester), Some(new ScribeId("another-scribe"))], view.Calendar.Select(entry => entry.PlacedBy));
    }

    [Fact]
    public async Task Anoint_WithNoneAnointed_MakesTheDraftNext_AndRecordsTheDecisionWithItsNote()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);

        Ok(await testbed.Scribes().Anoint(fair.Id, "  The Heretical Truth names the real fix.  ", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Fair", "Excellent", "Good"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        // Only it joins the Scribes' order: the ones it passed stay the Augury's.
        Assert.Equal([true, false, false], view.Calendar.Select(entry => entry.PlacedByScribes));
        var decision = Assert.Single(view.Decisions);
        Assert.Equal(ScribeAction.Anoint, decision.Action);
        Assert.Equal(fair.Id, decision.DraftId);
        Assert.Equal("Fair", decision.Title);
        Assert.Equal(Some(3), decision.CalendarPlace);
        Assert.Equal(Some(1), decision.CalendarPlaceAfter);
        Assert.Equal(Some(3), decision.AuguryPlace);
        Assert.Equal(Some(0.4f), decision.AuguryQuality);
        Assert.Equal(Some("The Heretical Truth names the real fix."), decision.Note);
        Assert.Equal(testbed.Time.GetUtcNow().UtcDateTime, decision.DecidedAtUtc);
    }

    [Fact]
    public async Task Anoint_TheFirstDraft_KeepsItAheadOfABetterOneFromARefill()
    {
        var first = await testbed.AddDraft("The Augury's pick", 0.6f);
        Ok(await testbed.Scribes().Anoint(first.Id, "Yes, this one.", BacklogTestbed.Tester, CancellationToken.None));

        await testbed.AddDraft("Better, from a refill", 0.95f);

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["The Augury's pick", "Better, from a refill"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal(Some("Yes, this one."), Assert.Single(view.Decisions).Note);
    }

    // Anointed while others are, a draft goes after them, ahead of every draft the Augury orders; the drafts it passed
    // stay the Augury's, so a better one written later goes ahead of them.
    [Fact]
    public async Task Anoint_PlacesTheDraftAfterTheAnointed_AheadOfTheAugurys()
    {
        await testbed.AddDraft("Excellent", 0.9f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        await testbed.AddDraft("Best, from a refill", 0.99f);

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Good", "Fair", "Best, from a refill", "Excellent"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal([true, true, false, false], view.Calendar.Select(entry => entry.PlacedByScribes));
        var anointing = view.Decisions.First(decision => decision.DraftId == fair.Id);
        Assert.Equal((Some(3), Some(2)), (anointing.CalendarPlace, anointing.CalendarPlaceAfter));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None)));
    }

    // Published by the midnight run between reading the calendar and saving, a draft has nothing left to release: its
    // own state tells, since publishing leaves the order's ETag as it was.
    [Fact]
    public async Task Releasing_ADraftPublishedWhileTheRequestRuns_IsChangedMeanwhile_AndRecordsNothing()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var drafts = new InterruptedDrafts(testbed.Drafts, async () => Ok(await testbed.Publisher().PublishToday(CancellationToken.None)));
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), testbed.Time);

        var result = await scribes.Release(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(result));
        Assert.DoesNotContain(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions, decision => decision.Action == ScribeAction.Release);
    }

    // Released, an anointed draft is the Augury's again, which orders it with the rest; the decision is kept, with no
    // place after (the Augury's order decides it).
    [Fact]
    public async Task Release_GivesTheDraftBackToTheAugury()
    {
        await testbed.AddDraft("Excellent", 0.9f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        Ok(await testbed.Scribes().Release(fair.Id, "Not so sure now.", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Good", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.DoesNotContain(view.Calendar, entry => entry.PlacedByScribes);
        var release = Assert.Single(view.Decisions, decision => decision.Action == ScribeAction.Release);
        Assert.Equal((ScribeAction.Release, Some(1), Option<int>.None, Some("Not so sure now.")),
            (release.Action, release.CalendarPlace, release.CalendarPlaceAfter, release.Note));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Release(good.Id, "", BacklogTestbed.Tester, CancellationToken.None)));
    }

    [Fact]
    public async Task AnAnointingBetweenReadingTheOrderAndTheDrafts_IsNotLost()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Guid anointedMeanwhile = Guid.Empty;
        // Right after this request reads the waiting drafts, a refill adds one and another Scribe anoints it.
        var drafts = new InterruptedDrafts(testbed.Drafts, async () =>
        {
            var arrived = await testbed.AddDraft("Arrived meanwhile", 0.5f);
            anointedMeanwhile = arrived.Id;
            Ok(await testbed.Scribes().Anoint(arrived.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        });
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), testbed.Time);

        var result = await scribes.Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(result));
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(anointedMeanwhile, view.Calendar[0].Draft.Id);
    }

    [Fact]
    public async Task Reordering_ADraftBurnedWhileTheRequestRuns_IsChangedMeanwhile_AndRecordsNothing()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        // Right after this request reads the waiting drafts, another Scribe burns the one it's about to anoint.
        var drafts = new InterruptedDrafts(testbed.Drafts,
            async () => Ok(await testbed.Scribes().Burn(fair.Id, "Burned meanwhile.", BacklogTestbed.Tester, CancellationToken.None)));
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), testbed.Time);

        var result = await scribes.Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(result));
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(ScribeAction.Burn, Assert.Single(view.Decisions).Action);
        Assert.Equal(["Excellent"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
    }

    [Fact]
    public async Task Burning_ADraftAnointedWhileTheRequestRuns_BurnsIt_AndItsPlacementDoesNoHarm()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        // Fair isn't placed when this request reads the calendar; right after, another Scribe anoints it. The burn
        // still goes through (both Scribes acted on what they saw), and Fair stays in the Scribes' order, burned.
        var drafts = new InterruptedDrafts(testbed.Drafts,
            async () => Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None)));
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), testbed.Time);

        Ok(await scribes.Burn(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var burned = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent"], burned.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal(["Fair"], Ok(await testbed.Scribes().Ashes(CancellationToken.None)).Select(draft => draft.Writing.Title));
        Ok(await testbed.Scribes().Restore(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var restored = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], restored.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.False(restored.Calendar.Single(entry => entry.Draft.Id == fair.Id).PlacedByScribes);
    }

    [Fact]
    public async Task Reordering_WhenTheAnointedItSwapsWithIsBurnedMeanwhile_IsChangedMeanwhile_AndRecordsNothing()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(excellent.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        // Humbling Excellent would put Good first. Right after this request reads the waiting drafts, another Scribe
        // burns Good, which takes it out of the order and changes its ETag.
        var drafts = new InterruptedDrafts(testbed.Drafts,
            async () => Ok(await testbed.Scribes().Burn(good.Id, "Burned meanwhile.", BacklogTestbed.Tester, CancellationToken.None)));
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), testbed.Time);

        var result = await scribes.Humble(excellent.Id, "", BacklogTestbed.Tester, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(result));
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.DoesNotContain(view.Decisions, decision => decision.Action == ScribeAction.Humble);
        Assert.Equal([true, false], view.Calendar.Select(entry => entry.PlacedByScribes));
    }

    [Fact]
    public async Task ThePublisher_ReadsAConsistentCalendar_WhenADraftIsAnointedWhileItRuns()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        Guid anointedMeanwhile = Guid.Empty;
        var drafts = new InterruptedDrafts(testbed.Drafts, async () =>
        {
            var arrived = await testbed.AddDraft("Arrived meanwhile", 0.5f);
            anointedMeanwhile = arrived.Id;
            Ok(await testbed.Scribes().Anoint(arrived.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        });
        var publisher = new DailyMachineSpirit.Functions.Generation.DailyRitePublisher(
            testbed.Rites, testbed.Rites, drafts, testbed.Scriptorium, testbed.Refiller(), Options.Create(Settings.Augury), testbed.Time, testbed.Probe.Metrics,
            NullLogger<DailyMachineSpirit.Functions.Generation.DailyRitePublisher>.Instance);

        var published = Ok(await publisher.PublishToday(CancellationToken.None));

        // The anointing came after the publisher read the order: it publishes from what it read, and the anointed draft
        // is next.
        Assert.Equal("Excellent", published.Rite.Writing.Title);
        Assert.Equal(anointedMeanwhile, Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0].Draft.Id);
    }

    [Fact]
    public async Task ThePublisher_PublishesTheAnointedDraft_OverTheAugurysPick()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.Equal("Fair", published.Rite.Writing.Title);
        Assert.Equal("Excellent", Assert.Single(Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar).Draft.Writing.Title);
    }

    [Fact]
    public async Task Exalt_MovesAnAnointedDraftUpOne()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        await AnointAll(excellent, good, fair);

        Ok(await testbed.Scribes().Exalt(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair", "Good"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        var exalting = Assert.Single(view.Decisions, decision => decision.Action == ScribeAction.Exalt);
        Assert.Equal((None, Some(3), Some(2)), (exalting.Note, exalting.CalendarPlace, exalting.CalendarPlaceAfter));
    }

    [Fact]
    public async Task Humble_MovesAnAnointedDraftDownOne()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        await AnointAll(excellent, good, fair);

        Ok(await testbed.Scribes().Humble(excellent.Id, "Too close to last week's.", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Good", "Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal(Some(2), Assert.Single(view.Decisions, decision => decision.Action == ScribeAction.Humble).CalendarPlaceAfter);
    }

    // The Augury orders the drafts it holds: only the anointed are moved by hand, among themselves, and moving one never
    // places another. A page shown before a draft was released offers its arrows still, and is told it changed.
    [Fact]
    public async Task ExaltAndHumble_MoveOnlyAmongTheAnointed()
    {
        var w = await testbed.AddDraft("W", 0.9f);
        var x = await testbed.AddDraft("X", 0.8f);
        await testbed.AddDraft("Y", 0.7f);
        var z = await testbed.AddDraft("Z", 0.6f);
        await AnointAll(z, x);

        Ok(await testbed.Scribes().Exalt(x.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["X", "Z", "W", "Y"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal([true, true, false, false], view.Calendar.Select(entry => entry.PlacedByScribes));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Exalt(w.Id, "", BacklogTestbed.Tester, CancellationToken.None)));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Humble(w.Id, "", BacklogTestbed.Tester, CancellationToken.None)));
    }

    // At the ends of the anointed, there's nowhere to go: the first goes out next, and the last is followed by the
    // Augury's order.
    [Fact]
    public async Task ExaltingTheFirst_OrHumblingTheLastAnointed_ChangesNothing_RecordsNothing_AndSaysSo()
    {
        var first = await testbed.AddDraft("Excellent", 0.95f);
        var last = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);
        await AnointAll(first, last);

        Assert.Equal(Scribes.NothingToMove, Failed(await testbed.Scribes().Exalt(first.Id, "", BacklogTestbed.Tester, CancellationToken.None)));
        Assert.Equal(Scribes.NothingToMove, Failed(await testbed.Scribes().Humble(last.Id, "", BacklogTestbed.Tester, CancellationToken.None)));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Good", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.All(view.Decisions, decision => Assert.Equal(ScribeAction.Anoint, decision.Action));
    }

    [Fact]
    public async Task Burn_HidesTheDraftInTheAshes_KeepsIt_AndSaysHowManyStillWait()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Fair", 0.4f);

        var waiting = Ok(await testbed.Scribes().Burn(excellent.Id, "Not funny.", BacklogTestbed.Tester, CancellationToken.None));

        Assert.Equal(1, waiting);
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        var ash = Assert.Single(Ok(await testbed.Scribes().Ashes(CancellationToken.None)));
        Assert.Equal(DraftState.Burned, ash.State);
        Assert.Equal(Some(testbed.Time.GetUtcNow().UtcDateTime), ash.BurnedAtUtc);
        Assert.Equal(Some(BacklogTestbed.Tester), ash.BurnedBy);
        Assert.Equal(Some(1), ash.BurnedFromPlace);
        Assert.Equal(Some("Not funny."), ash.BurnReason);
        var decision = Assert.Single(view.Decisions);
        Assert.Equal(ScribeAction.Burn, decision.Action);
        Assert.Equal(Some("Not funny."), decision.Note);
        Assert.Equal(Some(1), decision.CalendarPlace);
    }

    [Fact]
    public async Task Restore_BringsADraftBackFromTheAshes()
    {
        var draft = await testbed.AddDraft("Second chance", 0.6f);
        Ok(await testbed.Scribes().Burn(draft.Id, "Too specific", BacklogTestbed.Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));

        Ok(await testbed.Scribes().Restore(draft.Id, "Funnier than I thought.", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal("Second chance", Assert.Single(view.Calendar).Draft.Writing.Title);
        var waiting = view.Calendar[0].Draft;
        Assert.True(waiting.BurnedAtUtc.IsNone && waiting.BurnedBy.IsNone && waiting.BurnedFromPlace.IsNone && waiting.BurnReason.IsNone);
        Assert.Empty(Ok(await testbed.Scribes().Ashes(CancellationToken.None)));
        var restored = view.Decisions[0];
        Assert.Equal(ScribeAction.Restore, restored.Action);
        Assert.Equal(None, restored.CalendarPlace);
        Assert.Equal(Some("Funnier than I thought."), restored.Note);
    }

    [Fact]
    public async Task Restore_ADraftThatWasAnointed_ReturnsItToTheAugurysOrder()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Burn(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        Ok(await testbed.Scribes().Restore(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        // "Excellent" stays where the anointing put it; only the restored draft lost its place.
        Assert.False(view.Calendar.Single(entry => entry.Draft.Writing.Title == "Fair").PlacedByScribes);
    }

    [Fact]
    public async Task Restore_WorksForADraftBurnedLongAgo_BeyondTheAshesShown()
    {
        var first = await testbed.AddDraft("Burned first", 0.5f);
        Ok(await testbed.Scribes().Burn(first.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        for (var i = 0; i < Settings.ScriptoriumPages.AshesShown; i++)
        {
            testbed.Time.Advance(TimeSpan.FromMinutes(1));
            var later = await testbed.AddDraft($"Burned later {i}", 0.5f);
            Ok(await testbed.Scribes().Burn(later.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        }
        Assert.DoesNotContain(Ok(await testbed.Scribes().Ashes(CancellationToken.None)), ash => ash.Id == first.Id);

        Ok(await testbed.Scribes().Restore(first.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        Assert.Equal("Burned first", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    [Fact]
    public async Task TwoScribesBurningTheLastTwo_AtOnce_AtLeastOneSeesNoneLeft()
    {
        var one = await testbed.AddDraft("One", 0.5f);
        var two = await testbed.AddDraft("Two", 0.5f);

        var left = await Task.WhenAll(
            testbed.Scribes().Burn(one.Id, "", BacklogTestbed.Tester, CancellationToken.None),
            testbed.Scribes().Burn(two.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        Assert.Contains(0, left.Select(Ok));
        Assert.Empty(await testbed.Waiting());
    }

    [Fact]
    public async Task LetTheAuguryDecide_ForgetsTheScribesOrder()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        Ok(await testbed.Scribes().LetTheAuguryDecide(BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.All(view.Calendar, entry => Assert.False(entry.PlacedByScribes));
    }

    [Fact]
    public async Task ANoteTooLong_IsRefused_AndNothingChanges()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);

        var result = await testbed.Scribes().Burn(draft.Id, new string('a', ScribeDecision.MaxNoteLength + 1), BacklogTestbed.Tester, CancellationToken.None);

        Assert.Equal(Scribes.NoteTooLong, Failed(result));
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task ActingOnADraftThatIsNotWaiting_IsChangedMeanwhile()
    {
        var unknown = Guid.NewGuid();

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Anoint(unknown, "", BacklogTestbed.Tester, CancellationToken.None)));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Burn(unknown, "", BacklogTestbed.Tester, CancellationToken.None)));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Restore(unknown, "", BacklogTestbed.Tester, CancellationToken.None)));
    }

    [Fact]
    public async Task TwoScribesPlacingAtOnce_OneWins_TheOtherIsToldItChanged()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        var stale = Ok(await testbed.Scriptorium.GetPlacements(CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        // The second Scribe's change was based on the order before the first one's.
        var second = await testbed.Scriptorium.SavePlacements(
            stale with { Order = [fair.Id] }, None, [], AuditEntries.Of(testbed.Time, BacklogTestbed.Tester, AuditAction.Anoint, None, None), CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(second));
        Assert.Equal("Good", Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0].Draft.Writing.Title);
    }

    [Fact]
    public async Task ARefill_AvoidsTheSubjectsOfBurnedDrafts()
    {
        testbed.BacklogSize = 1;
        var burned = await testbed.AddDraft("Litany of the Endless Spinner", 0.5f);
        Ok(await testbed.Scribes().Burn(burned.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Answer("Something new"));

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var instructions = testbed.Models.Requests.Single(request => request.Model == Sol).Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("* Litany of the Endless Spinner", instructions);
    }

    [Fact]
    public async Task View_WhenCosmosFails_ReturnsTheError()
    {
        var result = await testbed.ScribesWithoutCosmos().View(CancellationToken.None);

        Assert.True(result.IsLeft);
    }

    private async Task AnointAll(params Draft[] drafts)
    {
        foreach (var draft in drafts)
        {
            Ok(await testbed.Scribes().Anoint(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        }
    }
}
