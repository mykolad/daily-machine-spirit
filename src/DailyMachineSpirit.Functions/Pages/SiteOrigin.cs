using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The site's origin as the reader reached it, for addresses written out in full (canonical links, the sitemap, the rites
/// API's links): the domain in production, which Cloudflare passes on, and the app's own address on staging. HTTPS
/// whatever scheme the request reached the app with, since TLS may end before it.
/// </summary>
public static class SiteOrigin
{
    public static string Of(HttpRequest request) => $"https://{request.Host}";
}
