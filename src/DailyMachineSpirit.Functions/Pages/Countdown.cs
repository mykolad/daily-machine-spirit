using System.Globalization;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>Time to the next rite, published at 00:00 UTC: "3h 12m", or minutes alone in the last hour.</summary>
public static class Countdown
{
    public static string ToNextRite(DateTimeOffset now)
    {
        // Rounded up, so the last minute reads "1m" rather than "0m".
        var minutes = (int)Math.Ceiling((NextRiteAt(now) - now).TotalMinutes);
        return minutes >= 60 ? $"{minutes / 60}h {minutes % 60}m" : $"{minutes}m";
    }

    /// <summary>
    /// What the script finds a countdown by, holding when the next rite is due as of this render: once that's past, the
    /// page shows the day before, and the script reloads it when it's shown again (<see cref="Script"/>).
    /// </summary>
    public static string Attribute(DateTimeOffset now)
        => $"data-countdown=\"{NextRiteAt(now).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}\"";

    private static DateTimeOffset NextRiteAt(DateTimeOffset now) => new(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
}
