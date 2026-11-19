using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Archive (<c>/archive</c>): every rite, Today's first, a page at a time, grouped by month. With <c>?q=</c> it's
/// the search results: the rites holding every word, the words marked where they're found.
/// The same page either way, rendered on the server, so a search's address can be shared and works without the script.
/// </summary>
public static class ArchivePage
{
    private const string Description = "Every litany, newest first.";

    // Forgetting can't be undone, so it asks first, in place: the question is hidden until "Forget them…" is pressed,
    // and that is hidden while nothing is witnessed (the script shows it).
    private const string ForgetWitnessed = """
        <div class="witnessed-forget">
        <button type="button" class="forget-ask" data-forget-ask hidden>Forget them…</button>
        <div class="forget-question" role="group" aria-label="Forget the witnessed rites?" data-forget-question hidden>
        <p class="forget-text">Forget all <span data-witnessed>0</span>? Every rite will be unwitnessed again.</p>
        <div class="forget-choices"><button type="button" class="forget-keep" data-forget-keep>Keep</button><button type="button" class="forget-confirm" data-forget-witnessed>Forget</button></div>
        </div>
        </div>
        """;

    /// <param name="olderThan">Where the next page starts, or None when these are the oldest.</param>
    /// <param name="days">Every rite's number and day, newest first, Today's too: the same as the list, so the count,
    /// the months and the list agree.</param>
    /// <param name="places">The places of the rites the Reliquary keeps, each worn as a badge.</param>
    public static string Render(IReadOnlyList<Rite> rites, Option<int> olderThan, IReadOnlyList<RiteDay> days, IReadOnlyDictionary<int, int> places, DateTimeOffset now, SearchListing listing)
    {
        var first = days.HeadOrNone().Map(day => day.Number);
        var months = ArchiveMonth.Of(days);
        // Below 1280px there are no margins, so the count moves into the line under the heading.
        var sub = days.Count == 0
            ? Description
            : $"""<span class="sub-wide">{Description}</span><span class="sub-narrow">{Rites(days.Count)}, newest first.</span>""";
        var since = days.LastOrNone().Match(
            Some: oldest => ArchiveSheet.Note("Since", $"""<p class="margin-since">{ImperialDate.WithTheDay(oldest.PublishedOnUtc, "since-day", "since-date")}</p>"""),
            None: () => string.Empty);
        var left = $"""
            <aside class="marginalia" aria-label="About the Archive">
            {ArchiveSheet.Note("Rites kept", $"""<p class="margin-number">{Html.Count(days.Count)}</p>""")}
            {(days.Count == 0 ? string.Empty : $"""<a class="margin-draw" href="{Paths.Draw}">Draw a rite</a>""")}
            {Witnessed(days.Count)}
            {since}
            {ArchiveSheet.Countdown(now)}
            </aside>
            """;
        var right = ArchiveSheet.MonthIndex(months, month => first.Exists(number => number == month.Newest)
            ? $"{Paths.Archive}#{month.Anchor}"
            : $"{Paths.ArchiveBefore(month.Newest + 1)}#{month.Anchor}");
        return Layout.Page("The Archive", Description, listing, Section.Archive, $"""
            {Masthead.Render(isHeading: false)}
            {ArchiveSheet.Render(
                kicker: string.Empty,
                heading: "The Archive",
                sub: sub,
                search: ArchiveSheet.Search(string.Empty, None) + ArchiveSheet.Orders(byVeneration: false) + WitnessedLine(days.Count),
                body: ArchiveRows.Months(rites, [], ArchiveRows.NoVotes, places),
                below: ArchiveRows.More(olderThan.Map(Paths.ArchiveBefore), "Load older rites", "The archive ends here. Earlier knowledge is lost.", ArchiveSheet.Closing(days.Count)),
                left: left,
                right: right)}
            """);
    }

    /// <param name="typed">What the reader typed, shown back as the heading and in the field.</param>
    /// <param name="words">The words looked for, marked in the rows.</param>
    /// <param name="tag">The subject the search is narrowed to, if any.</param>
    /// <param name="places">The places of the rites the Reliquary keeps, each worn as a badge.</param>
    public static string Results(string typed, IReadOnlyList<string> words, Option<Tag> tag, SearchResults results, IReadOnlyDictionary<int, int> places, DateTimeOffset now)
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
            : ArchiveRows.Months(results.Shown, words, ArchiveRows.NoVotes, places);
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
        return Layout.Page(title, Description, SearchListing.Unlisted, Section.Archive, $"""
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

    // How many rites the reader has witnessed (#198), which only their browser knows: the same for everyone here, hidden,
    // for the script to count and show, with the toggle and the way to forget. Without the script, nothing of it shows.
    private static string Witnessed(int kept)
        => kept == 0
            ? string.Empty
            : $"""
                <div class="margin-witnessed" data-witnessed-note hidden>
                <p class="margin-label">Witnessed</p>
                <p class="witnessed-count"><span data-witnessed>0</span> of {Rites(kept)}</p>
                {HideWitnessed("Hide witnessed", string.Empty)}
                {ForgetWitnessed}
                </div>
                """;

    // The same, as one line on the sheet under the orders, for a screen too narrow for the margins: the stylesheet shows
    // one or the other, never both. The count joins the checkbox's words, since there's no label above it here.
    private static string WitnessedLine(int kept)
        => kept == 0
            ? string.Empty
            : $"""
                <div class="sheet-witnessed" data-witnessed-note hidden>
                {HideWitnessed("Hide witnessed rites", $"""<span class="witnessed-tally">· <span data-witnessed>0</span> of {Html.Count(kept)}</span>""")}
                {ForgetWitnessed}
                </div>
                """;

    // A real checkbox, out of sight, under a drawn box with a quill tick: checked hides the witnessed rows.
    private static string HideWitnessed(string words, string tally)
        => $"""<label class="witnessed-only"><input type="checkbox" data-unwitnessed-only><span class="witnessed-box" aria-hidden="true">{Svg.QuillTick}</span>{words}{(tally.Length == 0 ? string.Empty : " " + tally)}</label>""";
}
