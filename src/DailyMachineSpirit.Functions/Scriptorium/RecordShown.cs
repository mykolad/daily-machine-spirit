using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// What the Record shows: its entries, newest first, and how they're narrowed; or, for one draft's record, all of its
/// entries and the way to the draft as it is now. Each "none" is something that couldn't be read, or isn't there.
/// </summary>
public sealed record RecordShown(
    IReadOnlyList<AuditEntry> Entries,
    IReadOnlyList<ScribeName> ChosenNames,
    Option<TabCounts> Counts,
    Option<NameInTheRecord> LookingScribesName,
    Option<Guid> RecordOfDraft,
    Option<Draft> DraftAsItIsNow,
    RecordFilter Filter,
    Option<DateOnly> OlderDaysFrom)
{
    /// <summary>
    /// The entries read, as whole days: when as many were read as could be, the oldest day may go on past them, so it's
    /// left for <see cref="OlderDaysFrom"/>. A day with more entries than are read is shown as far as it goes, and the
    /// Record goes on from the day before.
    /// </summary>
    public static (IReadOnlyList<AuditEntry> Entries, Option<DateOnly> OlderDaysFrom) WholeDays(IReadOnlyList<AuditEntry> newest, int read)
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
