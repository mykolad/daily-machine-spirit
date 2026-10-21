namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// How long a page is kept, as its <c>Cache-Control</c>: <c>max-age</c> for browsers (a minute, for every page) and
/// <c>s-maxage</c> for Cloudflare. Every page may also be served stale for a week while the app answers with a 5xx or
/// not at all (<c>stale-if-error</c>), so readers keep the pages Cloudflare still holds through an outage.
/// </summary>
public sealed record EdgeLifetime
{
    /// <summary>A minute at Cloudflare too: for pages no purge can find when they change, such as the archive.</summary>
    public static readonly EdgeLifetime Brief = new("public, max-age=60, stale-if-error=604800");

    /// <summary>
    /// An hour at Cloudflare, for pages the app purges when they change (a vote, a new rite:
    /// <see cref="Paths.ShowingVotesOf"/>), so their requests reach the app only after a change. The hour is the most
    /// such a page can be stale when a purge fails.
    /// </summary>
    public static readonly EdgeLifetime UntilPurged = new("public, max-age=60, s-maxage=3600, stale-if-error=604800");

    private EdgeLifetime(string cacheControl)
    {
        CacheControl = cacheControl;
    }

    public string CacheControl { get; }
}
