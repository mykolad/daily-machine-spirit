using System.Globalization;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// A month of the Archive: its rubric over the rows, and its line in the month index, with how many rites it holds and
/// its newest, where a link to it starts the Archive.
/// </summary>
public sealed record ArchiveMonth(int Year, int Month, int Count, int Newest)
{
    /// <summary>"m-2026-10": the rubric's id, which the month index links to.</summary>
    public string Anchor => $"m-{Year:D4}-{Month:D2}";

    /// <summary>"October 2026".</summary>
    public string Label => $"{MonthName} {Year}";

    public string MonthName => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(Month);

    /// <summary>The months these rites fall in, newest first, as the rites are.</summary>
    public static List<ArchiveMonth> Of(IEnumerable<RiteDay> newestFirst)
        => newestFirst
            .GroupBy(day => (day.PublishedOnUtc.Year, day.PublishedOnUtc.Month))
            .Select(month => new ArchiveMonth(month.Key.Year, month.Key.Month, month.Count(), month.Max(day => day.Number)))
            .ToList();
}
