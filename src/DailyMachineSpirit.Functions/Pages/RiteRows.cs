using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Rites as the archive and a tag's page list them, and the link to the next page. "Load older rites" is a plain link,
/// so it works without a script and with a keyboard; with it, the page loads the next rows in place (<c>Script</c>).
/// </summary>
public static class RiteRows
{
    /// <param name="older">The next page, or None when these are the oldest.</param>
    /// <param name="end">Said instead of the link at the end.</param>
    public static string Render(IReadOnlyList<Rite> rites, Option<string> older, string end)
    {
        var rows = new StringBuilder();
        foreach (var rite in rites)
        {
            // The title is the row's link, stretched over the whole row, so the tags can be links of their own: a link
            // can't hold another. It comes first among the row's links, where the script moves focus.
            var tags = RiteTags.Of(rite.Similarity);
            var tagLinks = tags.Count == 0
                ? string.Empty
                : $"""<span class="row-tags"><span class="sr-only">Filed under </span>{TagLinks.Render(tags)}</span>""";
            rows.Append($"""
                <li class="row">
                <span class="row-meta"><time class="row-date" datetime="{Html.IsoDate(rite.PublishedOnUtc)}">{Html.ShortDate(rite.PublishedOnUtc)}</time><span class="kind">{(rite.Writing.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL")}</span></span>
                <span class="row-main"><a class="row-title" href="{Paths.Rite(rite.Number)}">{Html.Encode(rite.Writing.Title)}</a><span class="row-first">{Html.WithInlineCode(Html.FirstLine(rite.Writing.Text))}</span><span class="row-counts"><span>{Svg.SmallFlameIcon}{Html.Count(rite.BlessedCount)} Blessed</span><span>{Svg.SmallSkullIcon}{Html.Count(rite.HeresyCount)} Heresy</span></span>{tagLinks}</span>
                </li>
                """);
        }

        var more = older.Match(
            Some: href => $"""<a class="button button-outline" href="{Html.Encode(href)}">Load older rites</a>""",
            None: () => $"""<p class="archive-end">{end}</p>""");
        return $"""
            <ol class="rows">{rows}</ol>
            <div class="archive-more">{more}</div>
            """;
    }
}
