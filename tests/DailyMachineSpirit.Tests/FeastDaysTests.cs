using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Feasts;
using LanguageExt;
using Microsoft.Extensions.AI;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Rites written for the day they're published (#202): written ahead by a refill, published on their day.</summary>
public sealed class FeastDaysTests : IAsyncLifetime
{
    private static readonly DateOnly TwiceHour = new(2026, 10, 25);

    private static readonly AuguryOptions Augury = new() { RecentRitesCompared = 7, SameKindPenalty = 0.1f };

    private static int made;

    private readonly BacklogTestbed testbed = new();

    public ValueTask InitializeAsync() => testbed.InitializeAsync();

    public ValueTask DisposeAsync() => testbed.DisposeAsync();

    [Theory]
    [InlineData("2027-01-01", "old-year")]
    [InlineData("2028-02-29", "unexpected-day")]
    [InlineData("2026-09-13", "full-byte")]
    [InlineData("2028-09-12", "full-byte")]
    [InlineData("2026-11-13", "unlucky-release")]
    [InlineData("2027-07-30", "keepers")]
    [InlineData("2027-03-28", "lost-hour")]
    [InlineData("2026-10-25", "twice-hour")]
    [InlineData("2038-01-19", "last-second")]
    public void AFeast_FallsOnItsDay(string day, string feast)
        => Assert.Equal(Some(feast), Feasts.On(DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture)).Map(found => found.Key));

    [Theory]
    [InlineData("2026-10-18")]
    [InlineData("2027-07-23")]
    [InlineData("2026-03-22")]
    public void AnOrdinaryDay_HasNoFeast(string day)
        => Assert.True(Feasts.On(DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture)).IsNone);

    // The feast's draft takes its day; the order fills the days around it, and runs on past it.
    [Fact]
    public void PublishingDays_GiveAFeastItsDay_AndTheOrderTheDaysBetween()
    {
        var feast = Draft("For the hour", RiteKind.Prayer, None) with { Writing = Draft("For the hour", RiteKind.Prayer, None).Writing with { FeastDay = TwiceHour } };
        var waiting = new List<Draft> { Draft("First", RiteKind.Prayer, None), Draft("Second", RiteKind.Prayer, None), Draft("Third", RiteKind.Prayer, None), feast };

        var days = PublishingDays.Of(waiting, [], [], Augury, TwiceHour.AddDays(-1));

        Assert.Equal(
            [("First", TwiceHour.AddDays(-1)), ("For the hour", TwiceHour), ("Second", TwiceHour.AddDays(1)), ("Third", TwiceHour.AddDays(2))],
            days.Select(entry => (entry.Draft.Writing.Title, entry.Day)));
    }

    // The order after a feast is picked with the feast among the rites before it: after a prayer, a ritual of nearly the
    // same quality goes first, as the publisher will choose on that day.
    [Fact]
    public void PublishingDays_PickTheDayAfterAFeast_WithTheFeastBeforeIt()
    {
        var feast = Draft("For the hour", RiteKind.Prayer, None) with { Writing = Draft("For the hour", RiteKind.Prayer, None).Writing with { FeastDay = TwiceHour } };
        var waiting = new List<Draft> { Draft("A prayer", RiteKind.Prayer, Some(0.5f)), Draft("A ritual", RiteKind.Ritual, Some(0.48f)), feast };

        var days = PublishingDays.Of(waiting, [], [], Augury, TwiceHour);

        Assert.Equal(["For the hour", "A ritual", "A prayer"], days.Select(entry => entry.Draft.Writing.Title));
    }

    // A refill writes the coming feast's draft first, about the feast, keeping its day; once it waits, none other.
    [Fact]
    public async Task Refill_WritesAComingFeastsDraft_AboutIt_ForItsDay()
    {
        testbed.Time.SetUtcNow(new DateTimeOffset(2026, 10, 20, 0, 0, 5, TimeSpan.Zero));
        testbed.BacklogSize = 1;
        await testbed.AddDraft("Already waiting", 0.5f);
        testbed.Models.Answers(Sol, Answer("The Litany of the Twice-Run Job"));

        var written = Ok(await testbed.Refiller().Refill(CancellationToken.None));
        var again = Ok(await testbed.Refiller().Refill(CancellationToken.None));

        Assert.Equal((1, 0), (written, again));
        var feast = Assert.Single(await testbed.Waiting(), draft => draft.Writing.FeastDay.IsSome);
        Assert.Equal(("The Litany of the Twice-Run Job", Some(TwiceHour)), (feast.Writing.Title, feast.Writing.FeastDay));
        var instructions = testbed.Models.Requests.First(request => request.Model == Sol).Messages.Single(message => message.Role == ChatRole.System).Text;
        Assert.Contains("written for a feast day, and published on that day: the hour that happens twice", instructions);
    }

    // On its day the feast's draft goes out, though a better draft waits.
    [Fact]
    public async Task OnAFeastDay_ItsDraftIsPublished_AheadOfTheOrder()
    {
        testbed.Time.SetUtcNow(new DateTimeOffset(2026, 10, 25, 0, 0, 5, TimeSpan.Zero));
        await testbed.AddDraft("The best of drafts", 0.9f);
        await AddFeastDraft("For the hour", TwiceHour);

        var published = Ok(await testbed.Publisher().PublishToday(CancellationToken.None)).Rite;

        Assert.Equal(("For the hour", Some(TwiceHour)), (published.Writing.Title, published.Writing.FeastDay));
    }

    // The Scriptorium's Calendar shows each draft on the day it goes out: the feast's on its day, the order around it.
    [Fact]
    public async Task TheCalendar_ShowsTheFeastsDraftOnItsDay()
    {
        testbed.Time.SetUtcNow(new DateTimeOffset(2026, 10, 24, 0, 0, 5, TimeSpan.Zero));
        await testbed.AddDraft("Best", 0.9f);
        await testbed.AddDraft("Next best", 0.5f);
        await AddFeastDraft("For the hour", TwiceHour);

        var view = Ok(await testbed.Scribes().View(CancellationToken.None));

        Assert.Equal(
            [("Best", TwiceHour.AddDays(-1)), ("For the hour", TwiceHour), ("Next best", TwiceHour.AddDays(1))],
            view.Calendar.Select(entry => (entry.Draft.Writing.Title, entry.PublishesOnUtc)));
    }

    // With the feast's draft first, places count it: humbling the first anointed moves it from #2 to #3, and so it's
    // recorded.
    [Fact]
    public async Task AScribesMove_IsRecorded_InTheCalendarsPlaces_FeastsAmongThem()
    {
        testbed.Time.SetUtcNow(new DateTimeOffset(2026, 10, 25, 0, 0, 5, TimeSpan.Zero));
        var first = await testbed.AddDraft("First anointed", 0.5f);
        var second = await testbed.AddDraft("Second anointed", 0.5f);
        await AddFeastDraft("For the hour", TwiceHour);
        Ok(await testbed.Scribes().Anoint(first.Id, "", Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(second.Id, "", Tester, CancellationToken.None));

        Ok(await testbed.Scribes().Humble(first.Id, "", Tester, CancellationToken.None));

        var humbled = Ok(await testbed.Scriptorium.GetDecisions(10, CancellationToken.None)).First(decision => decision.Action == ScribeAction.Humble);
        Assert.Equal((Some(2), Some(3)), (humbled.CalendarPlace, humbled.CalendarPlaceAfter));
    }

    // The Augury's own places count the feasts too: with a feast next, anointing the Augury's first pick leaves it
    // where the Augury had it, #2, and records no disagreement.
    [Fact]
    public async Task AnointingTheAugurysPick_AfterAFeast_AgreesWithTheAugury()
    {
        testbed.Time.SetUtcNow(new DateTimeOffset(2026, 10, 25, 0, 0, 5, TimeSpan.Zero));
        var best = await testbed.AddDraft("Best", 0.9f);
        await testbed.AddDraft("Next best", 0.5f);
        await AddFeastDraft("For the hour", TwiceHour);

        Ok(await testbed.Scribes().Anoint(best.Id, "", Tester, CancellationToken.None));

        var anointed = Ok(await testbed.Scriptorium.GetDecisions(10, CancellationToken.None)).First(decision => decision.Action == ScribeAction.Anoint);
        Assert.Equal((Some(2), Some(2), Some(2)), (anointed.CalendarPlace, anointed.AuguryPlace, anointed.CalendarPlaceAfter));
    }

    // Unjudged: no order ranks a feast's draft.
    private async Task AddFeastDraft(string title, DateOnly day)
    {
        var draft = Draft(title, RiteKind.Prayer, None);
        Ok(await testbed.Drafts.Add(draft with { Writing = draft.Writing with { FeastDay = day } }, CancellationToken.None));
    }

    // Written in the order made, a minute apart, so drafts the Augury doesn't rank keep that order.
    private static Draft Draft(string title, RiteKind kind, Option<float> quality) => new()
    {
        Id = Guid.NewGuid(),
        State = DraftState.Waiting,
        Writing = new RiteWriting
        {
            Kind = kind,
            Title = title,
            Text = "Words.",
            HereticalTruth = "Truth.",
            GeneratedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(made++),
        },
        Augury = quality.Map(value => new Augury { Quality = value, JudgedBy = "rubric", JudgedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) }),
    };
}
