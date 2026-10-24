using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// What the Record is narrowed to, and the day it goes on from, from its address
/// (<c>?who=scribes&amp;what=moved&amp;from=2026-10-19</c>): a filter's links keep the day, so they narrow the days shown.
/// </summary>
/// <param name="From">The day to show from, back; none for the newest.</param>
public sealed record RecordFilter(RecordActors Who, Option<RecordKind> What, Option<DateOnly> From)
{
    public static RecordFilter Everything { get; } = new(RecordActors.Everyone, Option<RecordKind>.None, Option<DateOnly>.None);
}
