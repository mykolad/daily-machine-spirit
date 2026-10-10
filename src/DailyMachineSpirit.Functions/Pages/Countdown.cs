namespace DailyMachineSpirit.Functions.Pages;

/// <summary>Time to the next rite, published at 00:00 UTC: "3h 12m", or minutes alone in the last hour.</summary>
public static class Countdown
{
    public static string ToNextRite(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        var left = utc.Date.AddDays(1) - utc.UtcDateTime;
        // Rounded up, so the last minute reads "1m" rather than "0m".
        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        return minutes >= 60 ? $"{minutes / 60}h {minutes % 60}m" : $"{minutes}m";
    }
}
