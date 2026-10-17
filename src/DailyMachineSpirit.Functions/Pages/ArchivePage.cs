using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Archive (<c>/archive</c>): every rite but Today's, newest first, a page at a time, grouped by month. With
/// <c>?q=</c> it's the search results: the rites holding every word, Today's too, the words marked where they're found.
/// The same page either way, rendered on the server, so a search's address can be shared and works without the script.
/// </summary>
public static class ArchivePage
{
    public const int PageSize = 20;

    private const string Description = "Earlier litanies, newest first.";

    /// <param name="olderThan">Where the next page starts, or None when these are the oldest.</param>
    /// <param name="days">Every rite's number and day, newest first: Today's, then the Archive's.</param>
    public static string Render(IReadOnlyList<Rite> rites, Option<int> olderThan, IReadOnlyList<RiteDay> days, DateTimeOffset now)
    {
        // Today shows the newest rite, so the Archive's list and its months start with the one before.
        var listed = days.Skip(1).ToList();
        var first = listed.HeadOrNone().Map(day => day.Number);
        var months = ArchiveMonth.Of(listed);
        // Below 1280px there are no margins, so the count moves into the line under the heading.
        var sub = listed.Count == 0
            ? Description
            : $"""<span class="sub-wide">{Description}</span><span class="sub-narrow">{Rites(listed.Count)}, newest first.</span>""";
        var since = days.LastOrNone().Match(
            Some: oldest => ArchiveSheet.Note("Since", $"""<p class="margin-since">{Html.LongDate(oldest.PublishedOnUtc)}</p>"""),
            None: () => string.Empty);
        var left = $"""
            <aside class="marginalia" aria-label="About the Archive">
            {ArchiveSheet.Note("Rites kept", $"""<p class="margin-number">{Html.Count(days.Count)}</p>""")}
            {since}
            {ArchiveSheet.Countdown(now)}
            </aside>
            """;
        var right = ArchiveSheet.MonthIndex(months, month => first.Exists(number => number == month.Newest)
            ? $"{Paths.Archive}#{month.Anchor}"
            : $"{Paths.ArchiveBefore(month.Newest + 1)}#{month.Anchor}");
        return Layout.Page("The Archive", Description, Section.Archive, $"""
            {Masthead.Render(isHeading: false)}
            {ArchiveSheet.Render(
                kicker: string.Empty,
                heading: "The Archive",
                sub: sub,
                search: ArchiveSheet.Search(string.Empty),
                body: ArchiveRows.Months(rites, []),
                below: ArchiveRows.More(olderThan.Map(Paths.ArchiveBefore), "Load older rites", "The archive ends here. Earlier knowledge is lost.", ArchiveSheet.Closing(days.Count)),
                left: left,
                right: right)}
            """);
    }

    /// <param name="typed">What the reader typed, shown back as the heading and in the field.</param>
    /// <param name="words">The words looked for, marked in the rows.</param>
    public static string Results(string typed, IReadOnlyList<string> words, SearchResults results, DateTimeOffset now)
    {
        var quoted = $"“{typed}”";
        var found = results.Found.Count;
        var first = results.Found.HeadOrNone().Map(day => day.Number);
        var sub = found switch
        {
            0 => "No rite mentions it.",
            1 => "1 rite mentions it.",
            _ => $"{Html.Count(found)} rites mention it, newest first.",
        };
        var body = found == 0
            ? $"""
                <div class="sheet-none">
                <p>The Machine Spirit keeps its silence.<br>Try another word, or fewer of them.</p>
                <a class="sheet-button" href="{Paths.Archive}">Browse the whole Archive</a>
                </div>
                """
            : ArchiveRows.Months(results.Shown, words);
        var below = found == 0
            ? string.Empty
            : ArchiveRows.More(results.OlderThan.Map(number => Paths.ArchiveSearchBefore(typed, number)), "More results", "No more rites mention it.", string.Empty);
        var left = $"""
            <aside class="marginalia" aria-label="About this search">
            {ArchiveSheet.Note("Sought", $"""<p class="margin-sought">{Html.Encode(quoted)}</p>""")}
            {ArchiveSheet.Note("Found", $"""<p class="margin-number">{Rites(found)}</p>""")}
            <a class="margin-clear" href="{Paths.Archive}">Clear search</a>
            {ArchiveSheet.Countdown(now)}
            </aside>
            """;
        var right = ArchiveSheet.MonthIndex(ArchiveMonth.Of(results.Found), month => first.Exists(number => number == month.Newest)
            ? $"{Paths.ArchiveSearch(typed)}#{month.Anchor}"
            : $"{Paths.ArchiveSearchBefore(typed, month.Newest + 1)}#{month.Anchor}");
        // The count is in the title too, which a screen reader reads first when the results load.
        return Layout.Page($"{quoted}: {(found == 0 ? "no rites" : Rites(found))}", Description, Section.Archive, $"""
            {Masthead.Render(isHeading: false)}
            {ArchiveSheet.Render(
                kicker: "Search the Archive",
                heading: Html.Encode(quoted),
                sub: sub,
                search: ArchiveSheet.Search(typed),
                body: body,
                below: below,
                left: left,
                right: right)}
            """);
    }

    private static string Rites(int count) => count == 1 ? "1 rite" : $"{Html.Count(count)} rites";
}
