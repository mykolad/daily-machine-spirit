namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Archive's page in Today's design: one parchment sheet of rows, the margins beside it on a wide screen, and the
/// candles, glow and traces of Today's altar. The markup stays in reading order (the sheet, then what's below it, then
/// the margins) and the stylesheet places it, as on Today.
/// </summary>
public static class ArchiveSheet
{
    /// <param name="kicker">The small red line over the heading, or empty.</param>
    /// <param name="heading">The page's h1, as HTML.</param>
    /// <param name="sub">The line under it, as HTML.</param>
    /// <param name="search">The sheet's own search field, or empty.</param>
    /// <param name="body">The rows, or what's said when there are none.</param>
    /// <param name="below">What follows the sheet: the link to older rites, or the end.</param>
    /// <param name="margins">The asides beside the sheet on a wide screen.</param>
    public static string Render(string kicker, string heading, string sub, string search, string body, string below, string margins)
        => $"""
            <div class="hero archive-hero">
            <div class="sheet">
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
            {margins}
            {Svg.AltarCandles}
            {Svg.Traces}
            </div>
            """;

    /// <summary>
    /// The sheet's search field: a plain form, so it works without the script. Its clear button is a link back to the
    /// whole Archive.
    /// </summary>
    public static string Search(string typed)
        => $"""
            <form class="sheet-search" role="search" action="{Paths.Archive}" method="get">
            {Svg.SearchIcon}
            <input type="search" id="archive-search" name="q" value="{Html.Encode(typed)}" aria-label="Search the Archive" placeholder="Search the Archive" maxlength="{SearchWords.MaxLength}" autocomplete="off" spellcheck="false">
            {(typed.Length == 0 ? string.Empty : $"""<a class="sheet-clear" href="{Paths.Archive}" aria-label="Clear search">{Svg.ClearIcon}</a>""")}
            </form>
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
}
