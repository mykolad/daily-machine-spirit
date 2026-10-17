using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DailyMachineSpirit.Tests;

/// <summary>Requests as a Scribe's browser makes them, and what the Scriptorium answers.</summary>
public static class ScriptoriumRequests
{
    public static HttpRequest Get(string query)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "GET";
        if (query.Length > 0)
        {
            request.QueryString = new QueryString(query);
        }
        return request;
    }

    /// <summary>A page's form, posted from the same site, url-encoded.</summary>
    public static HttpRequest Form(params (string Name, string Value)[] fields)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "POST";
        request.Host = new HostString("dailymachinespirit.fyi");
        request.Headers["Sec-Fetch-Site"] = "same-origin";
        request.ContentType = "application/x-www-form-urlencoded";
        var body = string.Join("&", fields.Select(field => $"{field.Name}={Uri.EscapeDataString(field.Value)}"));
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return request;
    }

    /// <summary>Where a redirect leads, as the browser asks for it: the address without its #fragment.</summary>
    public static HttpRequest Following(IActionResult redirect)
        => Get(new Uri(new Uri("https://dailymachinespirit.fyi"), Assert.IsType<RedirectResult>(redirect).Url).Query);

    public static string RedirectedTo(IActionResult result) => Assert.IsType<RedirectResult>(result).Url;

    public static string Html(IActionResult result, int status)
    {
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(status, content.StatusCode);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        return content.Content ?? string.Empty;
    }
}
