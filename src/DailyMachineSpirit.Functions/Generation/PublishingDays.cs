using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>
/// The day each waiting draft goes out (#202): a feast's draft on its feast's day, the Calendar's order on the days
/// between. What the publisher takes, and what the Scriptorium's Calendar shows.
/// </summary>
public static class PublishingDays
{
    /// <summary>
    /// The drafts kept for a feast still to come, from <paramref name="first"/> on: one a day, the oldest written. Any
    /// other (a feast whose day passed, or a second draft for a day) takes its turn in the Calendar's order.
    /// </summary>
    public static IReadOnlyDictionary<DateOnly, Draft> Feasts(IReadOnlyList<Draft> waiting, DateOnly first)
        => waiting
            .Where(draft => draft.Writing.FeastDay.Exists(day => day >= first))
            .GroupBy(draft => draft.Writing.FeastDay.IfNone(first))
            .ToDictionary(day => day.Key, day => day.OrderBy(draft => draft.Writing.GeneratedAtUtc).ThenBy(draft => draft.Id).First());

    /// <summary>The waiting drafts the Calendar orders: every one not kept for a feast.</summary>
    public static List<Draft> Ordinary(IReadOnlyList<Draft> waiting, IReadOnlyDictionary<DateOnly, Draft> feasts)
    {
        var kept = feasts.Values.Select(draft => draft.Id).ToHashSet();
        return waiting.Where(draft => !kept.Contains(draft.Id)).ToList();
    }

    /// <summary>Day by day from <paramref name="first"/>: the day's feast draft, or else the next in the order.</summary>
    /// <param name="ordered">The ordinary drafts, in the Calendar's order.</param>
    public static List<(Draft Draft, DateOnly Day)> Of(IReadOnlyList<Draft> ordered, IReadOnlyDictionary<DateOnly, Draft> feasts, DateOnly first)
    {
        var next = new Queue<Draft>(ordered);
        var last = feasts.Keys.DefaultIfEmpty(first).Max();
        var days = new List<(Draft Draft, DateOnly Day)>();
        for (var day = first; next.Count > 0 || day <= last; day = day.AddDays(1))
        {
            if (feasts.TryGetValue(day, out var feast))
            {
                days.Add((feast, day));
            }
            else if (next.Count > 0)
            {
                days.Add((next.Dequeue(), day));
            }
        }
        return days;
    }
}
