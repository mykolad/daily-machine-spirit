using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>What the Record shows: its entries, newest first, and how they're narrowed.</summary>
/// <param name="Names">The names the Scribes chose (#84); the others show by the end of their ids.</param>
/// <param name="Name">How the Record shows the Scribe looking; none when the names can't be read.</param>
/// <param name="Draft">One draft's record, when it's that; its entries are then all of that draft's.</param>
/// <param name="Found">That draft as it is now, to lead to it; none when it can't be read.</param>
/// <param name="OlderFrom">The day the Record goes on from, past the days shown; none when there's nothing older.</param>
public sealed record RecordShown(
    IReadOnlyList<AuditEntry> Entries,
    IReadOnlyList<ScribeName> Names,
    Option<TabCounts> Counts,
    Option<NameInTheRecord> Name,
    Option<Guid> Draft,
    Option<Draft> Found,
    RecordFilter Filter,
    Option<DateOnly> OlderFrom)
{
    /// <summary>
    /// The entries read, as whole days: when as many were read as could be, the oldest day may go on past them, so it's
    /// left for <see cref="OlderFrom"/>. A day with more entries than are read is shown as far as it goes, and the Record
    /// goes on from the day before.
    /// </summary>
    public static (IReadOnlyList<AuditEntry> Entries, Option<DateOnly> OlderFrom) WholeDays(IReadOnlyList<AuditEntry> newest, int read)
    {
        if (newest.Count < read)
        {
            return (newest, Option<DateOnly>.None);
        }
        var oldestDay = DateOnly.FromDateTime(newest[^1].AtUtc);
        var whole = newest.Where(entry => DateOnly.FromDateTime(entry.AtUtc) != oldestDay).ToList();
        return whole.Count == 0
            ? (newest, Some(oldestDay.AddDays(-1)))
            : (whole, Some(oldestDay));
    }
}
