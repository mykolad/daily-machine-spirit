using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>How the Scriptorium's functions read forms and answer.</summary>
public static class ScriptoriumHttp
{
    public static IActionResult Html(HttpRequest request, string html, int status)
    {
        // The Scribes' page is never cached (by Cloudflare or the browser) and never indexed. It's never shown inside
        // another site's frame either, where disguised buttons could trick a Scribe into burning or reordering
        // (clickjacking); X-Frame-Options covers browsers without frame-ancestors.
        var headers = request.HttpContext.Response.Headers;
        headers.CacheControl = "no-store";
        headers["X-Robots-Tag"] = "noindex";
        headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        headers.XFrameOptions = "DENY";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }

    // A 302, which browsers follow with a GET.
    public static IActionResult Redirect(string url) => new RedirectResult(url) { PreserveMethod = false, Permanent = false };

    // The page's forms are always url-encoded; anything else is the client's mistake (400), not the Scriptorium's.
    public static async Task<Option<IFormCollection>> ReadForm(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return None;
        }
        try
        {
            return Some(await request.ReadFormAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is (InvalidDataException or IOException) && !cancellationToken.IsCancellationRequested)
        {
            return None;
        }
    }
}
