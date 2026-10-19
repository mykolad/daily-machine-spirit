using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Telemetry;

/// <summary>
/// What the site's people do, one log line each, for the activity dashboard (observability/README.md): a Scribe's
/// action, a reader's vote, sign-in or search. Counted from logs, not metrics: these are rare, and a counter's first
/// value in a process, often its only one, is never seen as an increase. Each attribute names what was done, never who
/// did it or what they searched for.
/// </summary>
public static class SiteActivity
{
    /// <param name="action">What the Scribe did, as the Scriptorium's routes name it: anoint, burn, add-precept...</param>
    public static void ScribeActed(this ILogger logger, string action)
        => logger.LogInformation("A Scribe acted: {ScribeAction}.", action);

    /// <param name="vote">blessed, heresy, or withdrawn.</param>
    public static void ReaderVoted(this ILogger logger, string vote)
        => logger.LogInformation("A reader voted: {Vote}.", vote);

    /// <param name="search">words, or words-in-a-subject.</param>
    public static void ReaderSearched(this ILogger logger, string search)
        => logger.LogInformation("A reader searched the Archive: {Search}.", search);

    /// <param name="outcome">As <c>dms.signins</c> counts it: signed-in, declined, refused or failed.</param>
    public static void ReaderSignedIn(this ILogger logger, string provider, string outcome)
        => logger.LogInformation("A reader's sign-in with {Provider}: {SignIn}.", provider, outcome);
}
