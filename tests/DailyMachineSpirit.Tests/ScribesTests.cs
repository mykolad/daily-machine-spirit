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

    // A move places the drafts it passes as well as the one moved: whoever moved it placed them, unless another Scribe
    // already had. The one moved is always the mover's.
    [Fact]
    public async Task Moves_KeepWhoPlacedEachDraft()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        var poor = await testbed.AddDraft("Poor", 0.2f);

        Ok(await testbed.Scribes().Exalt(good.Id, "", "first-scribe", CancellationToken.None));
        Ok(await testbed.Scribes().Humble(excellent.Id, "", "second-scribe", CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(poor.Id, "", "third-scribe", CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Poor", "Good", "Fair", "Excellent"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal(
            [Some("third-scribe"), Some("first-scribe"), Some("second-scribe"), Some("second-scribe")],
            view.Calendar.Select(entry => entry.PlacedBy));
    }

    // A draft burned or published leaves the order, and who placed it with it; the Augury's drafts have no placer.
    [Fact]
    public async Task WhoPlacedADraft_LeavesTheOrderWithIt()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Exalt(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));

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
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Exalt(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Ok(await testbed.Scribes().Exalt(fair.Id, "", "another-scribe", CancellationToken.None));

        var placements = Ok(await testbed.Scriptorium.GetPlacements(CancellationToken.None));
        Assert.DoesNotContain(good.Id, placements.Order);
        Assert.DoesNotContain(good.Id, placements.PlacedBy.Keys);
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Fair", "Excellent"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal([Some("another-scribe"), Some(BacklogTestbed.Tester)], view.Calendar.Select(entry => entry.PlacedBy));
    }

    [Fact]
    public async Task Anoint_MakesTheDraftNext_AndRecordsTheDecisionWithItsNote()
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

    // The rites an anointed one passed stay the Augury's, so a better one written later goes ahead of them, and the
    // Scribes' earlier placings follow the anointed one, still in their order.
    [Fact]
    public async Task Anoint_LeavesTheRitesItPassedToTheAugury_AndTheScribesPlacingsInOrder()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.9f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Exalt(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        await testbed.AddDraft("Best, from a refill", 0.99f);

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Fair", "Good", "Excellent", "Best, from a refill"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal([true, true, true, false], view.Calendar.Select(entry => entry.PlacedByScribes));
        Ok(await testbed.Scribes().LetTheAuguryDecide(BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(excellent.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var anointed = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Best, from a refill", "Good", "Fair"], anointed.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal([true, false, false, false], anointed.Calendar.Select(entry => entry.PlacedByScribes));
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

        var result = await scribes.Exalt(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(result));
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(anointedMeanwhile, view.Calendar[0].Draft.Id);
    }

    [Fact]
    public async Task Reordering_ADraftBurnedWhileTheRequestRuns_IsChangedMeanwhile_AndRecordsNothing()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        // Right after this request reads the waiting drafts, another Scribe burns the one it's about to exalt.
        var drafts = new InterruptedDrafts(testbed.Drafts,
            async () => Ok(await testbed.Scribes().Burn(fair.Id, "Burned meanwhile.", BacklogTestbed.Tester, CancellationToken.None)));
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), testbed.Time);

        var result = await scribes.Exalt(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None);

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
        Ok(await testbed.Scribes().Restore(fair.Id, "", BacklogTestbed.Tester, None, CancellationToken.None));
        var restored = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], restored.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.False(restored.Calendar.Single(entry => entry.Draft.Id == fair.Id).PlacedByScribes);
    }

    [Fact]
    public async Task Reordering_WhenADraftItPassesIsBurnedMeanwhile_IsChangedMeanwhile_AndRecordsNothing()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);
        // Humbling Excellent would place Good first. Right after this request reads the waiting drafts, another Scribe
        // burns Good, which wasn't placed, so the order's ETag doesn't change: only Good's own state can tell.
        var drafts = new InterruptedDrafts(testbed.Drafts,
            async () => Ok(await testbed.Scribes().Burn(good.Id, "Burned meanwhile.", BacklogTestbed.Tester, CancellationToken.None)));
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), testbed.Time);

        var result = await scribes.Humble(excellent.Id, "", BacklogTestbed.Tester, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(result));
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(ScribeAction.Burn, Assert.Single(view.Decisions).Action);
        Assert.DoesNotContain(view.Calendar, entry => entry.PlacedByScribes);
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
            testbed.Rites, drafts, testbed.Scriptorium, testbed.Refiller(), Options.Create(Settings.Augury), testbed.Time, testbed.Probe.Metrics,
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
    public async Task Exalt_MovesTheDraftUpOne()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);

        Ok(await testbed.Scribes().Exalt(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair", "Good"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal(None, Assert.Single(view.Decisions).Note);
    }

    [Fact]
    public async Task Humble_MovesTheDraftDownOne()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);

        Ok(await testbed.Scribes().Humble(excellent.Id, "Too close to last week's.", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Good", "Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Equal(ScribeAction.Humble, Assert.Single(view.Decisions).Action);
    }

    [Fact]
    public async Task Moving_KeepsTheScribesEarlierOrder_BelowTheMovedDraft()
    {
        var w = await testbed.AddDraft("W", 0.9f);
        var x = await testbed.AddDraft("X", 0.8f);
        await testbed.AddDraft("Y", 0.7f);
        var z = await testbed.AddDraft("Z", 0.6f);
        Ok(await testbed.Scribes().Anoint(z.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        // The Scribes now want Y before X, against the Augury's order.
        Ok(await testbed.Scribes().Humble(x.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        Ok(await testbed.Scribes().Exalt(w.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["W", "Z", "Y", "X"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
    }

    [Fact]
    public async Task ExaltingTheFirst_OrHumblingTheLast_ChangesNothing_RecordsNothing_AndSaysSo()
    {
        var first = await testbed.AddDraft("Excellent", 0.95f);
        var last = await testbed.AddDraft("Fair", 0.4f);

        Assert.Equal(Scribes.NothingToMove, Failed(await testbed.Scribes().Exalt(first.Id, "", BacklogTestbed.Tester, CancellationToken.None)));
        Assert.Equal(Scribes.NothingToMove, Failed(await testbed.Scribes().Humble(last.Id, "", BacklogTestbed.Tester, CancellationToken.None)));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.Empty(view.Decisions);
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
        var decision = Assert.Single(view.Decisions);
        Assert.Equal(ScribeAction.Burn, decision.Action);
        Assert.Equal(Some("Not funny."), decision.Note);
        Assert.Equal(Some(1), decision.CalendarPlace);
    }

    [Fact]
    public async Task Restore_BringsADraftBackFromTheAshes()
    {
        var draft = await testbed.AddDraft("Second chance", 0.6f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));

        Ok(await testbed.Scribes().Restore(draft.Id, "Funnier than I thought.", BacklogTestbed.Tester, None, CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal("Second chance", Assert.Single(view.Calendar).Draft.Writing.Title);
        Assert.True(view.Calendar[0].Draft.BurnedAtUtc.IsNone);
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

        Ok(await testbed.Scribes().Restore(fair.Id, "", BacklogTestbed.Tester, None, CancellationToken.None));

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

        Ok(await testbed.Scribes().Restore(first.Id, "", BacklogTestbed.Tester, None, CancellationToken.None));

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
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Restore(unknown, "", BacklogTestbed.Tester, None, CancellationToken.None)));
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
}
