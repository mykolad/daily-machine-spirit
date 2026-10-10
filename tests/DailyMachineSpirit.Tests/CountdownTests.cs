using DailyMachineSpirit.Functions.Pages;

namespace DailyMachineSpirit.Tests;

public class CountdownTests
{
    [Theory]
    [InlineData("2026-10-09T20:48:00Z", "3h 12m")]
    [InlineData("2026-10-09T00:00:00Z", "24h 0m")]
    [InlineData("2026-10-09T23:15:00Z", "45m")]
    [InlineData("2026-10-09T23:59:30Z", "1m")]
    // A local time is counted to midnight UTC, not local midnight.
    [InlineData("2026-10-09T22:00:00+03:00", "5h 0m")]
    public void ToNextRite_CountsToMidnightUtc(string now, string expected)
        => Assert.Equal(expected, Countdown.ToNextRite(DateTimeOffset.Parse(now)));
}
