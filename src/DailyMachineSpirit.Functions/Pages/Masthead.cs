namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The site's name and tagline at the top of Today, the Archive and the Catechism, in place of the small brand link.
/// </summary>
public static class Masthead
{
    public const string Tagline = "In the grim darkness of the far future, no one reads the code.";

    /// <param name="isHeading">
    /// On Today the site's name is the page's h1; on the Archive and the Catechism the page has its own.
    /// </param>
    public static string Render(bool isHeading)
    {
        var name = isHeading ? "<h1 class=\"site-name\">The Daily Machine Spirit</h1>" : "<p class=\"site-name\">The Daily Machine Spirit</p>";
        return $"""
            <div class="masthead">
            {Svg.MastheadRule}
            {name}
            <p class="tagline">{Tagline}</p>
            </div>
            """;
    }
}
