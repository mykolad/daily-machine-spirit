namespace DailyMachineSpirit.Mcp;

/// <summary>
/// What the dashboard counts, one log line each, as the site counts its own: a counter would miss most calls, which
/// come a few a day to instances that start for one and stop (observability/README.md). Nothing about the caller.
/// </summary>
public static class LogCounters
{
    public const string Answered = "answered";

    public const string NoSuchRite = "no-such-rite";

    public const string Unavailable = "unavailable";

    /// <param name="tool">The tool's name: list_rites or reveal_heretical_truth.</param>
    /// <param name="outcome"><see cref="Answered"/>, <see cref="NoSuchRite"/> or <see cref="Unavailable"/> (the API failed).</param>
    public static void CountToolCall(this ILogger logger, string tool, string outcome, TimeSpan duration)
        => logger.LogInformation("Counted: a tool call, {Tool}, {Outcome}, in {DurationMs} ms.",
            tool, outcome, (long)duration.TotalMilliseconds);
}
