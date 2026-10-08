using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DailyMachineSpirit.Tests;

public class FontsFunctionTests
{
    [Theory]
    [InlineData("cinzel.woff2")]
    [InlineData("eb-garamond.woff2")]
    [InlineData("eb-garamond-italic.woff2")]
    [InlineData("figtree.woff2")]
    [InlineData("jetbrains-mono.woff2")]
    public void EveryFontTheStylesheetNames_IsServed_AndCachedForAYear(string name)
    {
        var context = new DefaultHttpContext();

        var font = Assert.IsType<FileContentResult>(new FontsFunction().Get(context.Request, name));

        Assert.Equal("font/woff2", font.ContentType);
        Assert.Equal("wOF2", System.Text.Encoding.ASCII.GetString(font.FileContents, 0, 4));
        Assert.Equal("public, max-age=31536000, immutable", context.Response.Headers.CacheControl.ToString());
        Assert.Contains($"url(/fonts/{name})", Styles.Css);
    }

    [Theory]
    [InlineData("comic-sans.woff2")]
    [InlineData("OFL.txt")]
    public void AnythingElse_IsNotFound(string name)
        => Assert.IsType<NotFoundResult>(new FontsFunction().Get(new DefaultHttpContext().Request, name));
}
