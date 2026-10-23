using System.Text;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A rite's own page (<c>/r/&lt;number&gt;</c>): the link that's shared, and where the archive leads.</summary>
public static class RitePage
{
    /// <param name="more">Rites to read next; the section is left out when there are none (the site's first rite).</param>
    public static string Render(Rite rite, IReadOnlyList<Rite> more, DateTimeOffset countedAt)
        => Layout.Page(rite.Writing.Title, Html.FirstLine(rite.Writing.Text.Replace("`", string.Empty)), Section.Other, $"""
            <a class="back" href="{Paths.Archive}">{Svg.BackIcon}Back to the Archive</a>
            {RiteCard.Render(rite, ownPage: true, countedAt, marginNotes: string.Empty)}
            {MoreRites(more)}
            """);

    private static string MoreRites(IReadOnlyList<Rite> more)
    {
        if (more.Count == 0)
        {
            return string.Empty;
        }

        var cards = new StringBuilder();
        foreach (var other in more)
        {
            cards.Append($"""
                <li><a class="more-card" href="{Paths.Rite(other.Number)}">
                <span class="kind">{(other.Writing.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL")} · {ImperialDate.InALink(other.PublishedOnUtc, "more-date")}</span>
                <span class="more-title">{Html.Encode(other.Writing.Title)}</span>
                <span class="row-first">{Html.WithInlineCode(Html.FirstLine(other.Writing.Text))}</span>
                </a></li>
                """);
        }

        return $"""
            <section class="more" aria-labelledby="more-rites">
            <h2 id="more-rites">More rites</h2>
            {Svg.BrassDivider}
            <ul class="more-list">{cards}</ul>
            </section>
            """;
    }
}
