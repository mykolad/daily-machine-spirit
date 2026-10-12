using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions;

/// <summary>
/// Whether a form was posted from this site, not forged by another one riding the reader's cookie or address. Browsers
/// say where a request comes from in Sec-Fetch-Site; ones too old for that still send Origin with a form post, which must
/// then be this site. A request with neither is refused too.
/// </summary>
public static class RequestOrigin
{
    public static bool IsFromThisSite(HttpRequest request)
    {
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0)
        {
            return fetchSite == "same-origin";
        }
        return Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
            && string.Equals(origin.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }
}
