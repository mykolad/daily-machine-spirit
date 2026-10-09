using System.Collections.Frozen;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The site's fonts, served by the site itself (<c>/fonts/&lt;name&gt;.woff2</c>) rather than by Google, so a visit
/// tells no one else the visitor's address. They're the Latin subsets, embedded in the app (Fonts/, OFL.txt).
/// </summary>
public sealed class FontsFunction
{
    // A font never changes under its name: a new version gets a new file name, so browsers may keep these for a year.
    private const string CacheForever = "public, max-age=31536000, immutable";

    private static readonly FrozenDictionary<string, byte[]> Fonts = LoadFonts();

    [Function("Fonts")]
    public IActionResult Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "fonts/{name}")] HttpRequest request,
        string name)
    {
        if (!Fonts.TryGetValue(name, out var font))
            return new NotFoundResult();
        request.HttpContext.Response.Headers.CacheControl = CacheForever;
        return new FileContentResult(font, "font/woff2");
    }

    private static FrozenDictionary<string, byte[]> LoadFonts()
    {
        var assembly = typeof(FontsFunction).Assembly;
        const string prefix = "DailyMachineSpirit.Functions.Pages.Fonts.";
        return assembly.GetManifestResourceNames()
            .Where(resource => resource.StartsWith(prefix, StringComparison.Ordinal) && resource.EndsWith(".woff2", StringComparison.Ordinal))
            .ToFrozenDictionary(resource => resource[prefix.Length..], resource =>
            {
                using var stream = assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"The embedded font {resource} is missing.");
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            });
    }
}
