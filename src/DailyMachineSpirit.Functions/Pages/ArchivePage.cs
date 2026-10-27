using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Archive (<c>/archive</c>): every rite but Today's, newest first, a page at a time, grouped by month. With
/// <c>?q=</c> it's the search results: the rites holding every word, Today's too, the words marked where they're found.
/// The same page either way, rendered on the server, so a search's address can be shared and works without the script.
/// </summary>
public static class ArchivePage
{
    private const string Description = "Earlier litanies, newest first.";

    // The readers' most venerated rites, beside the whole record.
    private static readonly string Reliquary = $"""<p class="sheet-also"><a href="{Paths.Reliquary}">The Reliquary: the most venerated rites</a></p>""";

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
            Some: oldest => ArchiveSheet.Note("Since", $"""<p class="margin-since">{ImperialDate.WithTheDay(oldest.PublishedOnUtc, "since-day", "since-date")}</p>"""),
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
                search: ArchiveSheet.Search(string.Empty, None) + Reliquary,
                body: ArchiveRows.Months(rites, [], ArchiveRows.NoVotes),
                below: ArchiveRows.More(olderThan.Map(Paths.ArchiveBefore), "Load older rites", "The archive ends here. Earlier knowledge is lost.", ArchiveSheet.Closing(days.Count)),
                left: left,
                right: right)}
            """);
    }

    /// <param name="typed">What the reader typed, shown back as the heading and in the field.</param>
    /// <param name="words">The words looked for, marked in the rows.</param>
    /// <param name="tag">The subject the search is narrowed to, if any.</param>
    public static string Results(string typed, IReadOnlyList<string> words, Option<Tag> tag, SearchResults results, DateTimeOffset now)
    {
        var quoted = $"“{typed}”";
        var tagKey = tag.Map(filter => filter.Key);
        var found = results.Found.Count;
        var first = results.Found.HeadOrNone().Map(rite => rite.Number);
        var sub = found switch
        {
            0 => "No rite mentions it.",
            1 => "1 rite mentions it.",
            _ => $"{Html.Count(found)} rites mention it, newest first.",
        };
        // Narrowed to a subject, the way out is the same words in every subject, rather than the whole Archive.
        var elsewhere = tag.Match(
            Some: filter => $"""<a class="sheet-button" href="{Html.Encode(Paths.ArchiveSearch(typed))}">Search every subject</a>""",
            None: () => $"""<a class="sheet-button" href="{Paths.Archive}">Browse the whole Archive</a>""");
        var body = found == 0
            ? $"""
                <div class="sheet-none">
                <p>The Machine Spirit keeps its silence.<br>Try another word, or fewer of them.</p>
                {elsewhere}
                </div>
                """
            : ArchiveRows.Months(results.Shown, words, ArchiveRows.NoVotes);
        var below = found == 0
            ? string.Empty
            : ArchiveRows.More(results.OlderThan.Map(number => Paths.ArchiveSearchBefore(typed, tagKey, number)), "More results", "No more rites mention it.", string.Empty);
        var left = $"""
            <aside class="marginalia" aria-label="About this search">
            {ArchiveSheet.Note("Sought", $"""<p class="margin-sought">{Html.Encode(quoted)}</p>""")}
            {ArchiveSheet.Note("Found", $"""<p class="margin-number">{Rites(found)}</p>""")}
            <a class="margin-clear" href="{Paths.Archive}">Clear search</a>
            {ArchiveSheet.Countdown(now)}
            </aside>
            """;
        var months = ArchiveSheet.MonthIndex(ArchiveMonth.Of(results.Found.Select(rite => rite.Day).ToList()), month => first.Exists(number => number == month.Newest)
            ? $"{Paths.ArchiveSearch(typed, tagKey)}#{month.Anchor}"
            : $"{Paths.ArchiveSearchBefore(typed, tagKey, month.Newest + 1)}#{month.Anchor}");
        // The count is in the title too, which a screen reader reads first when the results load.
        var title = $"{quoted}{tag.Map(filter => $" in {filter.Label}").IfNone(string.Empty)}: {(found == 0 ? "no rites" : Rites(found))}";
        return Layout.Page(title, Description, Section.Archive, $"""
            {Masthead.Render(isHeading: false)}
            {ArchiveSheet.Render(
                kicker: "Search the Archive",
                heading: Html.Encode(quoted),
                sub: sub,
                search: ArchiveSheet.Search(typed, tag),
                body: body,
                below: below,
                left: left,
                right: months + Subjects(typed, tag, results.Found))}
            """);
    }

    // The subjects the found rites are filed under, the most first, each narrowing the same words to it: the way to filter
    // without the script, and a sense of what the words are about. The subject already chosen isn't offered again.
    private static string Subjects(string typed, Option<Tag> chosen, IReadOnlyList<FoundRite> found)
    {
        var subjects = found
            .SelectMany(rite => rite.Tags.Select(RiteTags.Find).Somes())
            .Where(tag => !chosen.Exists(filter => filter == tag))
            .GroupBy(tag => tag)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.Label, StringComparer.Ordinal)
            .Select(group => $"""<a class="tag" href="{Html.Encode(Paths.ArchiveSearch(typed, Some(group.Key.Key)))}">{Html.Encode(group.Key.Label)} ({group.Count()})</a>""")
            .ToList();
        return subjects.Count == 0
            ? string.Empty
            : $"""
                <section class="other-subjects" aria-labelledby="narrow">
                <h2 id="narrow">{(chosen.IsSome ? "Other subjects" : "Narrow to a subject")}</h2>
                <p class="tags">{string.Join(" ", subjects)}</p>
                </section>
                """;
    }

    private static string Rites(int count) => count == 1 ? "1 rite" : $"{Html.Count(count)} rites";
}
