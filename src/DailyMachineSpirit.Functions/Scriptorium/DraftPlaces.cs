namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// Where a waiting draft was when a Scribe decided on it (1 is next): in the calendar, and in the Augury's order alone.
/// The Augury ranks only the drafts its current judge scored; the rest it places by age.
/// </summary>
public sealed record DraftPlaces(int CalendarPlace, int AuguryPlace, bool AuguryRankedByQuality);
