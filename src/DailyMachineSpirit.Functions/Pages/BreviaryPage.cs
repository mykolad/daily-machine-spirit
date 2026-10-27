using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Breviary (<c>/breviary</c>), the signed-in reader's own book of rites: every rite they voted on, on the Archive's
/// sheet, in a chapter per subject (<see cref="BreviaryChapter"/>), each marked with their verdict. Each chapter shows
/// its newest rites and links to itself, whole. Narrowed to the Blessed or the Heresies by links, and to the rites
/// holding some words by its own search, so it works without the script. Kept like favourites: a Blessed vote is a
/// bookmark.
/// </summary>
public static class BreviaryPage
{
    private const string Title = "Your Breviary";

    private const string Description = "The rites you voted on, Blessed or Heresy.";

    // How many of a chapter's rites the whole Breviary shows; its link shows the rest.
    private const int NewestPerChapter = 3;

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
    /// <param name="chapter">The one chapter shown, whole, or None for every chapter's newest.</param>
    /// <param name="typed">What the reader searched for, shown back in the field, or empty.</param>
    /// <param name="words">The words looked for, from what was typed; none shows every rite.</param>
    public static string Render(
        IReadOnlyList<Rite> rites, IReadOnlyDictionary<int, Reaction> yours, Option<Reaction> only, Option<Tag> chapter, string typed, IReadOnlyList<string> words)
    {
        var blessed = yours.Values.Count(reaction => reaction == Reaction.Blessed);
        var heresies = yours.Count - blessed;
        var found = rites.Where(rite => SearchWords.FoundIn(rite, words)).ToList();
        var inChapter = found.Where(rite => chapter.ForAll(subject => BreviaryChapter.SubjectsOf(rite).Contains(subject))).ToList();
        var inChapterBlessed = inChapter.Count(rite => VerdictOn(rite, yours) == Reaction.Blessed);
        var shown = inChapter.Where(rite => HasVerdict(rite, yours, only)).ToList();
        // Every chapter, the one shown among them, so the index leads from one to the next.
        var chapters = BreviaryChapter.Of(found.Where(rite => HasVerdict(rite, yours, only)).ToList());
        var verdict = only.Map(reaction => reaction == Reaction.Blessed ? BlessedVerdict : HeresyVerdict);
        var key = chapter.Map(subject => subject.Key);
        var left = $"""
            <aside class="marginalia" aria-label="Your votes">
            {ArchiveSheet.Note("Blessed", $"""<p class="margin-number">{Html.Count(blessed)}</p>""")}
            {ArchiveSheet.Note("Heresy", $"""<p class="margin-number">{Html.Count(heresies)}</p>""")}
            </aside>
            """;
        // What the search found with the verdict shown, as the verdicts' links count what each would show. The count is
        // in the title too, which a screen reader reads first when the results load.
        var title = words.Count == 0
            ? chapter.Map(subject => $"{subject.Label} in your Breviary").IfNone(Title)
            : $"“{typed}” in {chapter.Map(subject => $"{subject.Label}, in ").IfNone(string.Empty)}your Breviary: {(shown.Count == 0 ? "no rites" : Rites(shown.Count))}";
        return Layout.Page(title, Description, Section.Breviary, ArchiveSheet.Render(
            kicker: "Rites you have judged",
            heading: Title,
            sub: Sub(chapter.IsSome ? inChapter.Count : yours.Count, chapter.IsSome, words.Count == 0 ? None : Some(shown.Count)),
            search: yours.Count == 0
                ? string.Empty
                : Search(typed, verdict, chapter) + Verdicts(only, key, typed, inChapter.Count, inChapterBlessed, inChapter.Count - inChapterBlessed),
            body: shown.Count == 0
                ? NothingShown(yours.Count, only, chapter, words.Count > 0, verdict)
                : chapter.IsSome ? Rows(shown, words, yours) : Chapters(BreviaryChapter.Of(shown), verdict, typed, words, yours),
            below: $"""
                <div class="voted-account">Kept for your GitHub account · <form method="post" action="{Html.Encode(Paths.SignOutFrom(Paths.Breviary))}"><button type="submit" class="text-button">Sign out</button></form></div>
                """,
            left: left,
            right: Index(chapters, chapter, verdict, typed)));
    }

    private static Reaction VerdictOn(Rite rite, IReadOnlyDictionary<int, Reaction> yours) => yours[rite.Number];

    private static bool HasVerdict(Rite rite, IReadOnlyDictionary<int, Reaction> yours, Option<Reaction> only)
        => only.ForAll(verdict => VerdictOn(rite, yours) == verdict);

    // What the reader has, in the chapter if one's shown, or what their search found among it.
    private static string Sub(int voted, bool inAChapter, Option<int> found)
        => found.Match(
            Some: count => count switch
            {
                0 => "None of your rites mention it.",
                1 => "1 of your rites mentions it.",
                _ => $"{Html.Count(count)} of your rites mention it.",
            },
            None: () => voted == 0 ? Description : $"{Rites(voted)} you voted on, {(inAChapter ? "newest first" : "by subject")}.");

    // A plain form, so it works without the script, which shows the results as the reader types (the Archive's field
    // does the same). The verdict and chapter shown come along, and clearing the words keeps them. The chapter's chip
    // under it, a link to the same words in every chapter, takes the chapter off, as the Archive's subject chip does.
    private static string Search(string typed, Option<string> verdict, Option<Tag> chapter)
    {
        var key = chapter.Map(subject => subject.Key);
        return $"""
            <form class="sheet-search" role="search" aria-label="Your Breviary" action="{Paths.Breviary}" method="get">
            {Svg.SearchIcon}
            {verdict.Map(only => $"""<input type="hidden" name="verdict" value="{only}">""").IfNone(string.Empty)}
            {key.Map(subject => $"""<input type="hidden" name="chapter" value="{Html.Encode(subject)}">""").IfNone(string.Empty)}
            <input type="search" id="breviary-search" name="q" value="{Html.Encode(typed)}" aria-label="Search your Breviary" placeholder="Search your Breviary" maxlength="{SearchWords.MaxLength}" autocomplete="off" spellcheck="false">
            {(typed.Length == 0 ? string.Empty : $"""<a class="sheet-clear" href="{Html.Encode(Paths.BreviaryShowing(verdict, key, string.Empty))}" aria-label="Clear search">{Svg.ClearIcon}</a>""")}
            </form>
            {chapter.Map(subject => $"""<p class="sheet-filter">Chapter <a class="filter-chip" href="{Html.Encode(Paths.BreviaryShowing(verdict, None, typed))}" aria-label="Show every chapter, not only {Html.Encode(subject.Label)}">{Html.Encode(subject.Label)}{Svg.ClearIcon}</a></p>""").IfNone(string.Empty)}
            """;
    }

    // Links, not a form: each is a page of its own, so it works without the script, and the current one says so. Each
    // keeps the chapter and the words searched for, and counts what it would show.
    private static string Verdicts(Option<Reaction> only, Option<string> chapter, string typed, int all, int blessed, int heresies)
        => $"""
            <nav class="verdicts" aria-label="Show">
            {Verdict(Paths.BreviaryShowing(None, chapter, typed), $"All {Html.Count(all)}", only.IsNone)}
            {Verdict(Paths.BreviaryShowing(Some(BlessedVerdict), chapter, typed), $"Blessed {Html.Count(blessed)}", only.Exists(verdict => verdict == Reaction.Blessed))}
            {Verdict(Paths.BreviaryShowing(Some(HeresyVerdict), chapter, typed), $"Heresy {Html.Count(heresies)}", only.Exists(verdict => verdict == Reaction.Heresy))}
            </nav>
            """;

    private static string Verdict(string href, string label, bool current)
        => $"""<a class="verdict" href="{Html.Encode(href)}"{Current(current)}>{label}</a>""";

    private static string Current(bool current) => current ? " aria-current=\"page\"" : string.Empty;

    // Every chapter's newest few, so the page stays short however many rites the reader keeps, and a link to the rest.
    private static string Chapters(
        IReadOnlyList<BreviaryChapter> chapters, Option<string> verdict, string typed, IReadOnlyList<string> words, IReadOnlyDictionary<int, Reaction> yours)
    {
        var html = new StringBuilder("""<div class="chapters">""");
        foreach (var chapter in chapters)
        {
            html.Append($"""
                <section class="chapter" aria-labelledby="{chapter.Anchor}">
                <h2 class="chapter-rubric" id="{chapter.Anchor}"><span>{Html.Encode(chapter.Subject.Label)}</span></h2>
                {Rows(chapter.Rites.Take(NewestPerChapter).ToList(), words, yours)}
                """);
            if (chapter.Rites.Count > NewestPerChapter)
            {
                html.Append($"""<p class="chapter-more"><a href="{Html.Encode(ChapterLink(chapter.Subject, verdict, typed))}">All {Html.Count(chapter.Rites.Count)} in {Html.Encode(chapter.Subject.Label)}<span aria-hidden="true"> →</span></a></p>""");
            }
            html.Append("</section>");
        }
        return html.Append("</div>").ToString();
    }

    private static string Rows(IReadOnlyList<Rite> rites, IReadOnlyList<string> words, IReadOnlyDictionary<int, Reaction> yours)
        => $"""<ol class="rows">{string.Concat(rites.Select(rite => ArchiveRows.Row(rite, words, yours)))}</ol>""";

    private static string ChapterLink(Tag subject, Option<string> verdict, string typed) => Paths.BreviaryShowing(verdict, Some(subject.Key), typed);

    // The right margin: a link to each chapter, whole, with how many rites it holds; the one shown says so.
    private static string Index(IReadOnlyList<BreviaryChapter> chapters, Option<Tag> shown, Option<string> verdict, string typed)
        => chapters.Count == 0
            ? string.Empty
            : $"""
                <aside class="chapter-index" aria-label="Chapters">
                <p class="margin-label" aria-hidden="true">Chapters</p>
                <ul>{string.Concat(chapters.Select(chapter => $"""<li><a href="{Html.Encode(ChapterLink(chapter.Subject, verdict, typed))}"{Current(shown.Exists(subject => subject == chapter.Subject))}><span>{Html.Encode(chapter.Subject.Label)}</span><span class="chapter-count">{Html.Count(chapter.Rites.Count)}<span class="sr-only"> {(chapter.Rites.Count == 1 ? "rite" : "rites")}</span></span></a></li>"""))}</ul>
                </aside>
                """;

    // Said instead of the rows: nothing voted on yet, nothing the search found, or nothing with the verdict, or in the
    // chapter, shown.
    private static string NothingShown(int voted, Option<Reaction> only, Option<Tag> chapter, bool searched, Option<string> verdict)
    {
        if (voted == 0)
        {
            return Nothing("You haven’t voted on a rite yet.<br>Bless one, or call it heresy, and it’s kept here.", Paths.Today, "Read today’s rite");
        }
        var whole = Paths.BreviaryShowing(verdict, None, string.Empty);
        if (searched)
        {
            return Nothing("The Machine Spirit keeps its silence.<br>Try another word, or fewer of them.", whole, "Show your whole Breviary");
        }
        var where = chapter.Map(subject => $" in {Html.Encode(subject.Label)}").IfNone(string.Empty);
        var said = only.Match(
            Some: reaction => reaction == Reaction.Blessed ? $"You haven’t blessed a rite{where} yet." : $"You haven’t called a rite heresy{where} yet.",
            None: () => $"You haven’t voted on a rite{where} yet.");
        return chapter.IsSome ? Nothing(said, whole, "Show your whole Breviary") : Nothing(said, Paths.Today, "Read today’s rite");
    }

    private static string Nothing(string said, string href, string button)
        => $"""
            <div class="sheet-none">
            <p>{said}</p>
            <a class="sheet-button" href="{Html.Encode(href)}">{button}</a>
            </div>
            """;

    private static string Rites(int count) => count == 1 ? "1 rite" : $"{Html.Count(count)} rites";
}
