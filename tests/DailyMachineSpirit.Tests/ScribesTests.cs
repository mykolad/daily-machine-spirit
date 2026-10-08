using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Scriptorium;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.AI;
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

        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Title));
        // Today's rite isn't out yet, so the first goes out tonight... which is today's.
        Assert.Equal([Today, Today.AddDays(1)], view.Calendar.Select(entry => entry.PublishesOnUtc));
        Assert.Equal([1, 2], view.Calendar.Select(entry => entry.AuguryPlace));
        Assert.All(view.Calendar, entry => Assert.False(entry.PlacedByScribes));
        Assert.Empty(view.Ashes);
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

    [Fact]
    public async Task Anoint_MakesTheDraftNext_AndRecordsTheDecisionWithItsNote()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);

        Ok(await testbed.Scribes().Anoint(fair.Id, "  The Heretical Truth names the real fix.  ", CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Fair", "Excellent", "Good"], view.Calendar.Select(entry => entry.Draft.Title));
        // The ones it jumped over keep their order, now as the Scribes'.
        Assert.All(view.Calendar, entry => Assert.True(entry.PlacedByScribes));
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
        Ok(await testbed.Scribes().Anoint(first.Id, "Yes, this one.", CancellationToken.None));

        await testbed.AddDraft("Better, from a refill", 0.95f);

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["The Augury's pick", "Better, from a refill"], view.Calendar.Select(entry => entry.Draft.Title));
        Assert.Equal(Some("Yes, this one."), Assert.Single(view.Decisions).Note);
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
            Ok(await testbed.Scribes().Anoint(arrived.Id, "", CancellationToken.None));
        });
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, testbed.Time);

        var result = await scribes.Exalt(fair.Id, "", CancellationToken.None);

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
            async () => Ok(await testbed.Scribes().Burn(fair.Id, "Burned meanwhile.", CancellationToken.None)));
        var scribes = new Scribes(drafts, testbed.Rites, testbed.Scriptorium, testbed.Time);

        var result = await scribes.Exalt(fair.Id, "", CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(result));
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(ScribeAction.Burn, Assert.Single(view.Decisions).Action);
        Assert.Equal(["Excellent"], view.Calendar.Select(entry => entry.Draft.Title));
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
            Ok(await testbed.Scribes().Anoint(arrived.Id, "", CancellationToken.None));
        });
        var publisher = new DailyMachineSpirit.Functions.Generation.DailyRitePublisher(
            testbed.Rites, drafts, testbed.Scriptorium, testbed.Refiller(), testbed.Time);

        var published = Ok(await publisher.PublishToday(CancellationToken.None));

        // The anointing came after the publisher read the order: it publishes from what it read, and the anointed draft
        // is next.
        Assert.Equal("Excellent", published.Rite.Title);
        Assert.Equal(anointedMeanwhile, Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0].Draft.Id);
    }

    [Fact]
    public async Task ThePublisher_PublishesTheAnointedDraft_OverTheAugurysPick()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", CancellationToken.None));

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None));

        Assert.Equal("Fair", published.Rite.Title);
        Assert.Equal("Excellent", Assert.Single(Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar).Draft.Title);
    }

    [Fact]
    public async Task Exalt_MovesTheDraftUpOne()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);

        Ok(await testbed.Scribes().Exalt(fair.Id, "", CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair", "Good"], view.Calendar.Select(entry => entry.Draft.Title));
        Assert.Equal(None, Assert.Single(view.Decisions).Note);
    }

    [Fact]
    public async Task Humble_MovesTheDraftDownOne()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);

        Ok(await testbed.Scribes().Humble(excellent.Id, "Too close to last week's.", CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Good", "Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Title));
        Assert.Equal(ScribeAction.Humble, Assert.Single(view.Decisions).Action);
    }

    [Fact]
    public async Task Moving_KeepsTheScribesEarlierOrder_BelowTheMovedDraft()
    {
        var w = await testbed.AddDraft("W", 0.9f);
        var x = await testbed.AddDraft("X", 0.8f);
        await testbed.AddDraft("Y", 0.7f);
        var z = await testbed.AddDraft("Z", 0.6f);
        Ok(await testbed.Scribes().Anoint(z.Id, "", CancellationToken.None));
        // The Scribes now want Y before X, against the Augury's order.
        Ok(await testbed.Scribes().Humble(x.Id, "", CancellationToken.None));

        Ok(await testbed.Scribes().Exalt(w.Id, "", CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["W", "Z", "Y", "X"], view.Calendar.Select(entry => entry.Draft.Title));
    }

    [Fact]
    public async Task ExaltingTheFirst_OrHumblingTheLast_ChangesNothing_AndRecordsNothing()
    {
        var first = await testbed.AddDraft("Excellent", 0.95f);
        var last = await testbed.AddDraft("Fair", 0.4f);

        Ok(await testbed.Scribes().Exalt(first.Id, "", CancellationToken.None));
        Ok(await testbed.Scribes().Humble(last.Id, "", CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Title));
        Assert.Empty(view.Decisions);
    }

    [Fact]
    public async Task Burn_HidesTheDraftInTheAshes_KeepsIt_AndSaysHowManyStillWait()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Fair", 0.4f);

        var waiting = Ok(await testbed.Scribes().Burn(excellent.Id, "Not funny.", CancellationToken.None));

        Assert.Equal(1, waiting);
        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Fair"], view.Calendar.Select(entry => entry.Draft.Title));
        var ash = Assert.Single(view.Ashes);
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
        Ok(await testbed.Scribes().Burn(draft.Id, "", CancellationToken.None));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));

        Ok(await testbed.Scribes().Restore(draft.Id, "Funnier than I thought.", CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal("Second chance", Assert.Single(view.Calendar).Draft.Title);
        Assert.True(view.Calendar[0].Draft.BurnedAtUtc.IsNone);
        Assert.Empty(view.Ashes);
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
        Ok(await testbed.Scribes().Anoint(fair.Id, "", CancellationToken.None));
        Ok(await testbed.Scribes().Burn(fair.Id, "", CancellationToken.None));

        Ok(await testbed.Scribes().Restore(fair.Id, "", CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Title));
        // "Excellent" stays where the anointing put it; only the restored draft lost its place.
        Assert.False(view.Calendar.Single(entry => entry.Draft.Title == "Fair").PlacedByScribes);
    }

    [Fact]
    public async Task Restore_WorksForADraftBurnedLongAgo_BeyondTheAshesShown()
    {
        var first = await testbed.AddDraft("Burned first", 0.5f);
        Ok(await testbed.Scribes().Burn(first.Id, "", CancellationToken.None));
        for (var i = 0; i < Scribes.Shown; i++)
        {
            testbed.Time.Advance(TimeSpan.FromMinutes(1));
            var later = await testbed.AddDraft($"Burned later {i}", 0.5f);
            Ok(await testbed.Scribes().Burn(later.Id, "", CancellationToken.None));
        }
        Assert.DoesNotContain(Ok(await testbed.Scribes().View(CancellationToken.None)).Ashes, ash => ash.Id == first.Id);

        Ok(await testbed.Scribes().Restore(first.Id, "", CancellationToken.None));

        Assert.Equal("Burned first", Assert.Single(await testbed.Waiting()).Title);
    }

    [Fact]
    public async Task TwoScribesBurningTheLastTwo_AtOnce_AtLeastOneSeesNoneLeft()
    {
        var one = await testbed.AddDraft("One", 0.5f);
        var two = await testbed.AddDraft("Two", 0.5f);

        var left = await Task.WhenAll(
            testbed.Scribes().Burn(one.Id, "", CancellationToken.None),
            testbed.Scribes().Burn(two.Id, "", CancellationToken.None));

        Assert.Contains(0, left.Select(Ok));
        Assert.Empty(await testbed.Waiting());
    }

    [Fact]
    public async Task LetTheAuguryDecide_ForgetsTheScribesOrder()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", CancellationToken.None));

        Ok(await testbed.Scribes().LetTheAuguryDecide(CancellationToken.None));

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));
        Assert.Equal(["Excellent", "Fair"], view.Calendar.Select(entry => entry.Draft.Title));
        Assert.All(view.Calendar, entry => Assert.False(entry.PlacedByScribes));
    }

    [Fact]
    public async Task ANoteTooLong_IsRefused_AndNothingChanges()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);

        var result = await testbed.Scribes().Burn(draft.Id, new string('a', ScribeDecision.MaxNoteLength + 1), CancellationToken.None);

        Assert.Equal(Scribes.NoteTooLong, Failed(result));
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task ActingOnADraftThatIsNotWaiting_IsChangedMeanwhile()
    {
        var unknown = Guid.NewGuid();

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Anoint(unknown, "", CancellationToken.None)));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Burn(unknown, "", CancellationToken.None)));
        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(await testbed.Scribes().Restore(unknown, "", CancellationToken.None)));
    }

    [Fact]
    public async Task TwoScribesPlacingAtOnce_OneWins_TheOtherIsToldItChanged()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        var stale = Ok(await testbed.Scriptorium.GetPlacements(CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(good.Id, "", CancellationToken.None));

        // The second Scribe's change was based on the order before the first one's.
        var second = await testbed.Scriptorium.SavePlacements(stale with { Order = [fair.Id] }, None, CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(second));
        Assert.Equal("Good", Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0].Draft.Title);
    }

    [Fact]
    public async Task ARefill_AvoidsTheSubjectsOfBurnedDrafts()
    {
        testbed.BacklogSize = 1;
        var burned = await testbed.AddDraft("Litany of the Endless Spinner", 0.5f);
        Ok(await testbed.Scribes().Burn(burned.Id, "", CancellationToken.None));
        testbed.Models.Answers(Sol, Answer("Something new"));

        Ok(await testbed.Refiller().Refill(CancellationToken.None));

        var instructions = testbed.Models.Requests.Single().Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("* Litany of the Endless Spinner", instructions);
    }

    [Fact]
    public async Task View_WhenCosmosFails_ReturnsTheError()
    {
        var result = await testbed.ScribesWithoutCosmos().View(CancellationToken.None);

        Assert.True(result.IsLeft);
    }
}
