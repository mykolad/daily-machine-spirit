using System.Text;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Breviary (<c>/breviary</c>), the signed-in reader's own book of rites: every rite they voted on, on the Archive's
/// sheet, in a chapter per subject (<see cref="BreviaryChapter"/>), each marked with their verdict. Narrowed to the
/// Blessed or the Heresies by links, and to the rites holding some words by its own search, so it works without the
/// script. Kept like favourites: a Blessed vote is a bookmark.
/// </summary>
public static class BreviaryPage
{
    private const string Title = "Your Breviary";

    private const string Description = "The rites you voted on, Blessed or Heresy.";

    public const string BlessedVerdict = "blessed";

    public const string HeresyVerdict = "heresy";

    /// <summary>For a reader who isn't signed in: what the page keeps, and the way to sign in.</summary>
    public static string SignedOut()
        => Layout.Page(Title, Description, Section.Breviary, ArchiveSheet.Render(
            kicker: "Rites you have judged",
            heading: Title,
            sub: Description,
            search: string.Empty,
            body: $"""
                <div class="sheet-none">
                <p>Sign in with GitHub, and every rite you bless,<br>or call heresy, is kept here.</p>
                <a class="sheet-button" href="{Html.Encode(Paths.SignIn(Paths.Breviary))}">Sign in with GitHub</a>
                </div>
                """,
            below: string.Empty,
            left: string.Empty,
            right: string.Empty));

    /// <param name="rites">The rites voted on, newest first, as each chapter keeps them.</param>
    /// <param name="yours">The reader's vote on each.</param>
    /// <param name="only">The one verdict shown, or None for both.</param>
    /// <param name="typed">What the reader searched for, shown back in the field, or empty.</param>
    /// <param name="words">The words looked for, from what was typed; none shows every rite.</param>
    public static string Render(IReadOnlyList<Rite> rites, IReadOnlyDictionary<int, Reaction> yours, Option<Reaction> only, string typed, IReadOnlyList<string> words)
    {
        var blessed = yours.Values.Count(reaction => reaction == Reaction.Blessed);
        var heresies = yours.Count - blessed;
        var found = rites.Where(rite => SearchWords.FoundIn(rite, words)).ToList();
        var foundBlessed = found.Count(rite => VerdictOn(rite, yours) == Reaction.Blessed);
        var chapters = BreviaryChapter.Of(found.Where(rite => only.ForAll(verdict => VerdictOn(rite, yours) == verdict)).ToList());
        var verdict = only.Map(reaction => reaction == Reaction.Blessed ? BlessedVerdict : HeresyVerdict);
        var left = $"""
            <aside class="marginalia" aria-label="Your votes">
            {ArchiveSheet.Note("Blessed", $"""<p class="margin-number">{Html.Count(blessed)}</p>""")}
            {ArchiveSheet.Note("Heresy", $"""<p class="margin-number">{Html.Count(heresies)}</p>""")}
            </aside>
            """;
        // The count is in the title too, which a screen reader reads first when the results load.
        var title = words.Count == 0 ? Title : $"“{typed}” in your Breviary: {(found.Count == 0 ? "no rites" : Rites(found.Count))}";
        return Layout.Page(title, Description, Section.Breviary, ArchiveSheet.Render(
            kicker: "Rites you have judged",
            heading: Title,
            sub: Sub(yours.Count, words.Count == 0 ? None : Some(found.Count)),
            search: yours.Count == 0 ? string.Empty : Search(typed, verdict) + Verdicts(only, typed, found.Count, foundBlessed, found.Count - foundBlessed),
            body: chapters.Count > 0 ? Chapters(chapters, words, yours) : NothingShown(yours.Count, only, words.Count > 0, verdict),
            below: $"""
                <div class="voted-account">Kept for your GitHub account · <form method="post" action="{Html.Encode(Paths.SignOutFrom(Paths.Breviary))}"><button type="submit" class="text-button">Sign out</button></form></div>
                """,
            left: left,
            right: Index(chapters)));
    }

    private static Reaction VerdictOn(Rite rite, IReadOnlyDictionary<int, Reaction> yours) => yours[rite.Number];

    // What the reader has, or what their search found among it.
    private static string Sub(int voted, Option<int> found)
        => found.Match(
            Some: count => count switch
            {
                0 => "None of your rites mention it.",
                1 => "1 of your rites mentions it.",
                _ => $"{Html.Count(count)} of your rites mention it.",
            },
            None: () => voted == 0 ? Description : $"{Rites(voted)} you voted on, by subject.");

    // A plain form, so it works without the script, which shows the results as the reader types (the Archive's field
    // does the same). The verdict shown comes along, and clearing the words keeps it.
    private static string Search(string typed, Option<string> verdict)
        => $"""
            <form class="sheet-search" role="search" action="{Paths.Breviary}" method="get">
            {Svg.SearchIcon}
            {verdict.Map(key => $"""<input type="hidden" name="verdict" value="{key}">""").IfNone(string.Empty)}
            <input type="search" id="breviary-search" name="q" value="{Html.Encode(typed)}" aria-label="Search your Breviary" placeholder="Search your Breviary" maxlength="{SearchWords.MaxLength}" autocomplete="off" spellcheck="false">
            {(typed.Length == 0 ? string.Empty : $"""<a class="sheet-clear" href="{Html.Encode(Paths.BreviaryShowing(verdict, string.Empty))}" aria-label="Clear search">{Svg.ClearIcon}</a>""")}
            </form>
            """;

    // Links, not a form: each is a page of its own, so it works without the script, and the current one says so. Each
    // keeps the words searched for, and counts what it would show.
    private static string Verdicts(Option<Reaction> only, string typed, int all, int blessed, int heresies)
        => $"""
            <nav class="verdicts" aria-label="Show">
            {Verdict(Paths.BreviaryShowing(None, typed), $"All {Html.Count(all)}", only.IsNone)}
            {Verdict(Paths.BreviaryShowing(Some(BlessedVerdict), typed), $"Blessed {Html.Count(blessed)}", only.Exists(verdict => verdict == Reaction.Blessed))}
            {Verdict(Paths.BreviaryShowing(Some(HeresyVerdict), typed), $"Heresy {Html.Count(heresies)}", only.Exists(verdict => verdict == Reaction.Heresy))}
            </nav>
            """;

    private static string Verdict(string href, string label, bool current)
        => $"""<a class="verdict" href="{Html.Encode(href)}"{(current ? " aria-current=\"page\"" : string.Empty)}>{label}</a>""";

    private static string Chapters(IReadOnlyList<BreviaryChapter> chapters, IReadOnlyList<string> words, IReadOnlyDictionary<int, Reaction> yours)
    {
        var html = new StringBuilder("""<div class="chapters">""");
        foreach (var chapter in chapters)
        {
            html.Append($"""
                <section class="chapter" aria-labelledby="{chapter.Anchor}">
                <h2 class="chapter-rubric" id="{chapter.Anchor}"><span>{Html.Encode(chapter.Subject.Label)}</span></h2>
                <ol class="rows">
                """);
            foreach (var rite in chapter.Rites)
            {
                html.Append(ArchiveRows.Row(rite, words, yours));
            }
            html.Append("</ol></section>");
        }
        return html.Append("</div>").ToString();
    }

    // The right margin: a link to each chapter, with how many rites it holds.
    private static string Index(IReadOnlyList<BreviaryChapter> chapters)
        => chapters.Count == 0
            ? string.Empty
            : $"""
                <aside class="chapter-index" aria-label="Chapters">
                <p class="margin-label" aria-hidden="true">Chapters</p>
                <ul>{string.Concat(chapters.Select(chapter => $"""<li><a href="#{chapter.Anchor}"><span>{Html.Encode(chapter.Subject.Label)}</span><span class="chapter-count">{Html.Count(chapter.Rites.Count)}<span class="sr-only"> {(chapter.Rites.Count == 1 ? "rite" : "rites")}</span></span></a></li>"""))}</ul>
                </aside>
                """;

    // Said instead of the rows: nothing voted on yet, nothing the search found, or nothing with the verdict shown.
    private static string NothingShown(int voted, Option<Reaction> only, bool searched, Option<string> verdict)
        => voted == 0
            ? $"""
                <div class="sheet-none">
                <p>You haven’t voted on a rite yet.<br>Bless one, or call it heresy, and it’s kept here.</p>
                <a class="sheet-button" href="{Paths.Today}">Read today’s rite</a>
                </div>
                """
            : searched
                ? $"""
                    <div class="sheet-none">
                    <p>The Machine Spirit keeps its silence.<br>Try another word, or fewer of them.</p>
                    <a class="sheet-button" href="{Html.Encode(Paths.BreviaryShowing(verdict, string.Empty))}">Show your whole Breviary</a>
                    </div>
                    """
                : $"""
                    <div class="sheet-none">
                    <p>{(only.Exists(reaction => reaction == Reaction.Blessed) ? "You haven’t blessed a rite yet." : "You haven’t called a rite heresy yet.")}</p>
                    <a class="sheet-button" href="{Paths.Today}">Read today’s rite</a>
                    </div>
                    """;

    private static string Rites(int count) => count == 1 ? "1 rite" : $"{Html.Count(count)} rites";
}
