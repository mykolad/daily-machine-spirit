using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// Rites as the Archive's page lists them, under a rubric per month: the Archive, search results and a tag's page.
/// "Load older rites" is a plain link to the next page, so it works without a script and with a keyboard; with it, the
/// page loads the next rows in place (<see cref="Script"/>), into the month they belong to.
/// </summary>
public static class ArchiveRows
{
    /// <summary>For the pages every reader sees alike, which mark no one's votes.</summary>
    public static readonly IReadOnlyDictionary<int, Reaction> NoVotes = new Dictionary<int, Reaction>();

    /// <param name="words">What a search looks for, marked wherever it's found; none for the Archive.</param>
    /// <param name="yours">The reader's own votes, marked on their rows; none but on their own page.</param>
    public static string Months(IReadOnlyList<Rite> rites, IReadOnlyList<string> words, IReadOnlyDictionary<int, Reaction> yours)
    {
        var html = new StringBuilder("""<div class="months">""");
        foreach (var month in rites.GroupBy(rite => (rite.PublishedOnUtc.Year, rite.PublishedOnUtc.Month)))
        {
            var (year, number) = month.Key;
            var rubric = new ArchiveMonth(year, number, month.Count(), month.Max(rite => rite.Number));
            // The year in Roman numerals is for the eye; a screen reader says it as a number.
            html.Append($"""
                <section class="month" aria-labelledby="{rubric.Anchor}">
                <h2 class="month-rubric" id="{rubric.Anchor}"><span>{rubric.MonthName} <span aria-hidden="true">{RomanNumeral.Of(year)}</span><span class="sr-only">{year}</span></span></h2>
                <ol class="rows">
                """);
            foreach (var rite in month)
            {
                html.Append(Row(rite, words, yours));
            }
            html.Append("</ol></section>");
        }
        return html.Append("</div>").ToString();
    }

    /// <param name="older">The next page, or None when these are the oldest.</param>
    /// <param name="moreLabel">The link's words ("Load older rites").</param>
    /// <param name="end">Said instead of the link at the end.</param>
    /// <param name="closing">What hangs from the sheet at the end (the Archive's seal), or empty.</param>
    public static string More(Option<string> older, string moreLabel, string end, string closing)
        => $"""<div class="archive-more">{older.Match(
            Some: href => $"""<a class="button button-outline" href="{Html.Encode(href)}">{moreLabel}</a>""",
            None: () => $"""{closing}<p class="archive-end">{end}</p>""")}</div>""";

    /// <summary>
    /// One rite's row. The title is the row's link, stretched over the whole row, so the tags can be links of their own: a
    /// link can't hold another. It comes first among the row's links, where the script moves focus.
    /// </summary>
    /// <param name="words">What a search looks for, marked wherever it's found, or none.</param>
    /// <param name="yours">The reader's own votes, marked on their rows.</param>
    public static string Row(Rite rite, IReadOnlyList<string> words, IReadOnlyDictionary<int, Reaction> yours)
    {
        var line = words.Count == 0 ? RowLine.Opening(rite) : RowLine.For(rite, words);
        var tags = RiteTags.Of(rite.Tags);
        var tagLinks = tags.Count == 0
            ? string.Empty
            : $"""<span class="row-tags"><span class="sr-only">Filed under </span>{TagLinks.Render(tags)}</span>""";
        return $"""
            <li class="row">
            <span class="row-meta">{ImperialDate.InALink(rite.PublishedOnUtc, "row-date")}<span class="kind">{(rite.Writing.Kind == RiteKind.Prayer ? "PRAYER" : "RITUAL")}</span><span class="row-number"><span aria-hidden="true">NO. </span><span class="sr-only">Rite </span>{rite.Number}</span></span>
            <span class="row-main"><a class="row-title" href="{Paths.Rite(rite.Number)}" title="{ImperialDate.Day(rite.PublishedOnUtc)}">{Highlight.Of(rite.Writing.Title, words)}</a><span class="row-line">{Highlight.Of(line, words)}</span>{RowVotes(rite, yours)}{tagLinks}</span>
            </li>
            """;
    }

    // The reader's own verdict counts the others who gave it ("You and 2 others blessed it"), so beside it only the
    // other verdict's count is left: no vote is said twice.
    private static string RowVotes(Rite rite, IReadOnlyDictionary<int, Reaction> yours)
    {
        var blessed = rite.BlessedCount > 0 ? $"<span>{Svg.SmallFlameIcon}Blessed {Html.Count(rite.BlessedCount)}</span>" : string.Empty;
        var heresies = rite.HeresyCount > 0 ? $"<span>{Svg.SmallSkullIcon}Heresy {Html.Count(rite.HeresyCount)}</span>" : string.Empty;
        if (!yours.TryGetValue(rite.Number, out var reaction))
        {
            return Counts(blessed + heresies);
        }

        return reaction == Reaction.Blessed
            ? $"""<span class="row-yours"><span class="row-verdict row-verdict-blessed">{Svg.SmallFlameIcon}{YouAnd(rite.BlessedCount)} blessed it</span>{Counts(heresies)}</span>"""
            : $"""<span class="row-yours"><span class="row-verdict row-verdict-heresy">{Svg.SmallSkullIcon}{YouAnd(rite.HeresyCount)} called it heresy</span>{Counts(blessed)}</span>""";
    }

    // Each count only when there is one, and no line at all for a rite nobody has voted on.
    private static string Counts(string counts) => counts.Length == 0 ? string.Empty : $"""<span class="row-counts">{counts}</span>""";

    // The count holds the reader's own vote too.
    private static string YouAnd(int votes) => (votes - 1) switch
    {
        <= 0 => "You",
        1 => "You and 1 other",
        var others => $"You and {Html.Count(others)} others",
    };
}
