using System.Text;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Earlier rites, newest first, a page at a time. "Load older rites" is a plain link to the next page, so it works
/// without a script and with a keyboard.
/// </summary>
public static class ArchivePage
{
    public const int PageSize = 6;

    /// <param name="olderThan">Where the next page starts, or None when these are the oldest.</param>
    public static string Render(IReadOnlyList<Rite> rites, Option<int> olderThan)
    {
        var rows = new StringBuilder();
        foreach (var rite in rites)
            rows.Append($"""
                <li><a class="row" href="{Paths.Rite(rite.Number)}">
                <span class="row-meta"><time class="row-date" datetime="{Html.IsoDate(rite.PublishedOnUtc)}">{Html.ShortDate(rite.PublishedOnUtc)}</time><span class="kind">{(rite.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL")}</span></span>
                <span class="row-main"><span class="row-title">{Html.Encode(rite.Title)}</span><span class="row-first">{Html.WithInlineCode(Html.FirstLine(rite.Text))}</span></span>
                </a></li>
                """);
        var more = olderThan.Match(
            Some: number => $"""<a class="button button-outline" href="{Paths.ArchiveBefore(number)}">Load older rites</a>""",
            None: () => """<p class="archive-end">The archive ends here. Earlier knowledge is lost.</p>""");
        return Layout.Page("The Archive", "Earlier litanies, newest first.", Section.Archive, $"""
            <h1 class="archive-title">The Archive</h1>
            <p class="archive-subtitle">Earlier litanies, newest first.</p>
            <div class="archive-divider">{Svg.BrassDivider}</div>
            <ol class="rows">{rows}</ol>
            <div class="archive-more">{more}</div>
            """);
    }
}
