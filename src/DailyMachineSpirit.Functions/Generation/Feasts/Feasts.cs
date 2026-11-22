using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation.Feasts;

/// <summary>
/// The feast calendar (#202): the days a rite is written for, ahead, and published on. Where two fall on one day, the
/// earlier in the list is kept.
/// </summary>
public static class Feasts
{
    /// <summary>How far ahead a refill writes a feast's draft: time for the Scribes to read it, amend it or burn it.</summary>
    public const int DaysAhead = 14;

    public static readonly IReadOnlyList<Feast> All =
    [
        new("last-second", "The Feast of the Last Second",
            "the day the old clocks run out of seconds: time kept as a 32-bit count, which wraps around to a date long past",
            day => day == new DateOnly(2038, 1, 19)),
        new("old-year", "The Feast of the Old Year",
            "the first day of the year, still written as the old one: years typed by hand, years fixed in the code, and certificates and licences that ran out at midnight",
            day => day is { Month: 1, Day: 1 }),
        new("unexpected-day", "The Feast of the Unexpected Day",
            "the day the calendars didn't expect: code sure that February has 28 days and every year 365",
            day => day is { Month: 2, Day: 29 }),
        new("full-byte", "The Feast of the Full Byte",
            "the day of the full byte, the 256th of the year: counters that fill up and turn back to nothing",
            day => day.DayOfYear == 256),
        new("unlucky-release", "The Feast of the Unlucky Release",
            "the release nobody should make: deploying on a Friday, and on this Friday above all",
            day => day is { Day: 13, DayOfWeek: DayOfWeek.Friday }),
        new("keepers", "The Feast of the Keepers",
            "the keepers of the machines, thanked once a year: those who watch the servers at night, answer the alarms and restore what was lost",
            day => day.Month == 7 && day.DayOfWeek == DayOfWeek.Friday && day.AddDays(7).Month == 8),
        new("lost-hour", "The Feast of the Lost Hour",
            "the hour that never happens, when the clocks leap forward: jobs set for it that never run, and logs with an hour missing",
            day => day.Month == 3 && IsLastSunday(day)),
        new("twice-hour", "The Feast of the Hour That Happens Twice",
            "the hour that happens twice, when the clocks fall back: jobs that run twice, and logs that seem to go back in time",
            day => day.Month == 10 && IsLastSunday(day)),
    ];

    /// <summary>The feast that falls on the day, if any.</summary>
    public static Option<Feast> On(DateOnly day) => All.Find(feast => feast.Falls(day));

    /// <summary>The feast a rite was written for, if it went out on that day (one that missed it is an ordinary day's).</summary>
    public static Option<Feast> Kept(Rite rite) => rite.Writing.FeastDay.Filter(day => day == rite.PublishedOnUtc).Bind(On);

    private static bool IsLastSunday(DateOnly day) => day.DayOfWeek == DayOfWeek.Sunday && day.AddDays(7).Month != day.Month;
}
