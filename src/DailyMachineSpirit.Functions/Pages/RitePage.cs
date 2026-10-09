using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A rite's own page (<c>/r/&lt;number&gt;</c>): the link that's shared, and where the archive leads.</summary>
public static class RitePage
{
    public static string Render(Rite rite)
        => Layout.Page(rite.Title, Html.FirstLine(rite.Text.Replace("`", string.Empty)), Section.Other, $"""
            <a class="back" href="{Paths.Archive}">{Svg.BackIcon}Back to the Archive</a>
            {RiteCard.Render(rite, ownPage: true)}
            """);
}
