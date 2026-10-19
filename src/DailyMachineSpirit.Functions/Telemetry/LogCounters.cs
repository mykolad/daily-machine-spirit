using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Telemetry;

/// <summary>
/// Counters kept as log lines: each count is one Information line, and the activity dashboard (observability/README.md)
/// counts the lines by their attribute with Loki's <c>count_over_time</c>. Removing a line, or renaming its attribute
/// (<c>{ScribeAction}</c>), empties that dashboard's panel: they're its data, not chatter. For what the site's people
/// do, which is rare: a Scribe's action, a reader's vote, sign-in or Archive search. Each attribute names what was
/// done, never who did it or what they searched for.
/// <para>
/// Not a <see cref="SiteMetrics"/> counter, because one would miss most of them. Each process counts from zero and
/// sends its totals every minute; Prometheus's <c>increase()</c> measures how a total grew between samples, so a
/// series' first sample, with nothing before it, adds nothing. Processes come and go with traffic, and one that serves
/// a single vote sends a single 1: <c>increase()</c> reports 0. Grafana Cloud doesn't add the missing zero (tried on
/// staging, October 2026), and starting a counter at zero only helps if that zero is sent before the first event,
/// which usually comes with the very request that started the process. A log line is one event, however short its
/// process's life.
/// </para>
/// </summary>
public static class LogCounters
{
    /// <param name="action">What the Scribe did, as the Scriptorium's routes name it: anoint, burn, precept-add...</param>
    public static void CountScribeAction(this ILogger logger, string action)
        => logger.LogInformation("Counted: a Scribe's action, {ScribeAction}.", action);

    /// <param name="vote">blessed, heresy, or withdrawn.</param>
    public static void CountVote(this ILogger logger, string vote)
        => logger.LogInformation("Counted: a reader's vote, {Vote}.", vote);

    /// <param name="search">words, or words-in-a-subject.</param>
    public static void CountArchiveSearch(this ILogger logger, string search)
        => logger.LogInformation("Counted: a search of the Archive, {Search}.", search);

    /// <param name="outcome">As <c>dms.signins</c> counts it: signed-in, declined, refused or failed.</param>
    public static void CountSignIn(this ILogger logger, string provider, string outcome)
        => logger.LogInformation("Counted: a reader's sign-in with {Provider}, {SignIn}.", provider, outcome);
}
