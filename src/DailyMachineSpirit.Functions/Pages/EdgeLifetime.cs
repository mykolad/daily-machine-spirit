using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// How long a page is kept: a minute in browsers (<c>Cache-Control</c>), and at Cloudflare as long as its own header
/// says (<c>Cloudflare-CDN-Cache-Control</c>, which Cloudflare reads instead and never passes on). Cloudflare may also
/// serve a page stale for a week while the app answers with a 5xx (<c>stale-if-error</c>), so readers keep the pages it
/// still holds through an outage. Not in <c>Cache-Control</c> as <c>s-maxage</c>: Cloudflare takes that as
/// <c>proxy-revalidate</c>, which forbids serving stale.
/// </summary>
public sealed record EdgeLifetime
{
    private const string ForBrowsers = "public, max-age=60";

    /// <summary>A minute at Cloudflare too: for pages no purge can find when they change, such as the archive.</summary>
    public static readonly EdgeLifetime Brief = new("public, max-age=60, stale-if-error=604800");

    /// <summary>
    /// An hour at Cloudflare, for pages the app purges when they change (a vote, a new rite:
    /// <see cref="Paths.ShowingVotesOf"/>), so their requests reach the app only after a change. The hour is the most
    /// such a page can be stale when a purge fails.
    /// </summary>
    public static readonly EdgeLifetime UntilPurged = new("public, max-age=3600, stale-if-error=604800");

    private EdgeLifetime(string forCloudflare)
    {
        ForCloudflare = forCloudflare;
    }

    /// <summary>What Cloudflare reads, from <c>Cloudflare-CDN-Cache-Control</c>.</summary>
    public string ForCloudflare { get; }

    public void WriteTo(IHeaderDictionary headers)
    {
        headers.CacheControl = ForBrowsers;
        headers["Cloudflare-CDN-Cache-Control"] = ForCloudflare;
    }
}
