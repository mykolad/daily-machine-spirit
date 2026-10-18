using System.Globalization;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Archive's page in Today's design: one parchment sheet of rows, the margins beside it on a wide screen, and the
/// candles, glow and traces of Today's altar. The markup stays in reading order (the sheet, then what's below it, then
/// the margins) and the stylesheet places it, as on Today.
/// </summary>
public static class ArchiveSheet
{
    /// <summary>How many times a page repeats the code: enough for twenty rows and their month's rubric, and more.</summary>
    private const int CopiesPerPage = 12;

    // The margins' code, as Today's parchment has "READ THE CODE" in hex and "KNOW" in binary: "NOTHING IS FORGOTTEN"
    // and "KEPT", for the record. Repeated, as the sheet runs on for as long as there are rites, and cut where it ends:
    // a page's worth here, and the script adds a page's worth more with each page it loads (data-code, data-copies).
    private static readonly string LeftCode
        = string.Join(" ", "NOTHING IS FORGOTTEN".Select(letter => ((int)letter).ToString("X2", CultureInfo.InvariantCulture)));
    private static readonly string RightCode = string.Join(" ", "KEPT".Select(letter => Convert.ToString(letter, 2).PadLeft(8, '0')));

    // Dry words on the paper strip by the seal at the Archive's end, one for each count of rites, so a reader returning
    // another day may find another.
    private static readonly string[] Closings = ["Archived (probably)", "Backed up, never restored", "Retention: forever", "Read by no one since"];

    /// <param name="kicker">The small red line over the heading, or empty.</param>
    /// <param name="heading">The page's h1, as HTML.</param>
    /// <param name="sub">The line under it, as HTML.</param>
    /// <param name="search">The sheet's own search field, or empty.</param>
    /// <param name="body">The rows, or what's said when there are none.</param>
    /// <param name="below">What follows the sheet: the link to older rites, or the end.</param>
    /// <param name="left">The left margin on a wide screen.</param>
    /// <param name="right">The right margin on a wide screen, or empty.</param>
    public static string Render(string kicker, string heading, string sub, string search, string body, string below, string left, string right)
        => $"""
            <div class="hero archive-hero">
            <div class="sheet">
            <span class="rule-outer" aria-hidden="true"></span><span class="rule-inner" aria-hidden="true"></span>
            {Svg.Corners}
            <span class="margin-text margin-left" aria-hidden="true" data-code="{LeftCode}" data-copies="{CopiesPerPage}">{Repeated(LeftCode)}</span>
            <span class="margin-text margin-right" aria-hidden="true" data-code="{RightCode}" data-copies="{CopiesPerPage}">{Repeated(RightCode)}</span>
            <div class="sheet-inner">
            <header class="sheet-head">
            {(kicker.Length == 0 ? string.Empty : $"""<p class="sheet-kicker">{kicker}</p>""")}
            <h1 class="sheet-title">{heading}</h1>
            <p class="sheet-sub">{sub}</p>
            </header>
            {search}
            {body}
            </div>
            </div>
            {below}
            <div class="side side-left">
            {left}
            {Svg.AltarCandleLeft}
            </div>
            <div class="side side-right">
            {right}
            {Svg.AltarCandleRight}
            </div>
            {Svg.Traces}
            </div>
            """;

    /// <summary>
    /// The sheet's search field: a plain form, so it works without the script. Its clear button is a link back to the
    /// whole Archive. Searching within a subject, the form carries the tag along, and the chip under it, a link to the
    /// same words in every subject, takes the filter off.
    /// </summary>
    public static string Search(string typed, Option<Tag> tag)
        => $"""
            <form class="sheet-search" role="search" action="{Paths.Archive}" method="get">
            {Svg.SearchIcon}
            <input type="search" id="archive-search" name="q" value="{Html.Encode(typed)}" aria-label="Search the Archive" placeholder="Search the Archive" maxlength="{SearchWords.MaxLength}" autocomplete="off" spellcheck="false">
            {tag.Map(filter => $"""<input type="hidden" name="tag" value="{Html.Encode(filter.Key)}">""").IfNone(string.Empty)}
            {(typed.Length == 0 ? string.Empty : $"""<a class="sheet-clear" href="{Paths.Archive}" aria-label="Clear search">{Svg.ClearIcon}</a>""")}
            </form>
            {tag.Map(filter => $"""<p class="sheet-filter">Filed under <a class="filter-chip" href="{Html.Encode(Paths.ArchiveSearch(typed))}" aria-label="Remove filter: {Html.Encode(filter.Label)}">{Html.Encode(filter.Label)}{Svg.ClearIcon}</a></p>""").IfNone(string.Empty)}
            """;

    /// <summary>A margin's label and what it says.</summary>
    public static string Note(string label, string value)
        => $"""<div><p class="margin-label">{label}</p>{value}</div>""";

    /// <summary>The countdown to the next rite, as Today's left margin has it.</summary>
    public static string Countdown(DateTimeOffset now)
        => Note("Next litany in", $"""<p class="margin-value" data-countdown>{Pages.Countdown.ToNextRite(now)}</p>""");

    /// <summary>The right margin: a link per month, with how many rites it holds.</summary>
    public static string MonthIndex(IReadOnlyList<ArchiveMonth> months, Func<ArchiveMonth, string> link)
        => months.Count == 0
            ? string.Empty
            : $"""
                <aside class="month-index" aria-label="By month">
                <p class="margin-label" aria-hidden="true">By month</p>
                <ul>{string.Concat(months.Select(month => $"""<li><a href="{Html.Encode(link(month))}"><span>{month.Label}</span><span class="month-count">{Html.Count(month.Count)}<span class="sr-only"> {(month.Count == 1 ? "rite" : "rites")}</span></span></a></li>"""))}</ul>
                </aside>
                """;

    /// <summary>
    /// The end of the Archive, on its last page: the ribbon and wax seal hang from the sheet, as under Today's rite, with
    /// a dry word on a paper strip. Decoration, hidden from screen readers; the end's own words follow.
    /// </summary>
    /// <param name="rites">How many rites are kept, which picks the strip's words.</param>
    public static string Closing(int rites)
        => $"""
            <div class="archive-seal" aria-hidden="true">
            <div class="ribbon"><span></span><span></span></div>
            {Svg.ClosingSeal}
            <span class="purity-strip">{Closings[rites % Closings.Length]}</span>
            </div>
            """;

    private static string Repeated(string code) => string.Join(" · ", Enumerable.Repeat(code, CopiesPerPage));
}
