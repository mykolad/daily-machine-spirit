using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The signed-in reader's own rites (<c>/my-rites</c>): every rite they voted on, newest first, on the Archive's sheet,
/// each marked with their verdict, and narrowed to the Blessed or the Heresies by links, so it works without the
/// script. Kept like favourites: a Blessed vote is a bookmark.
/// </summary>
public static class VotedRitesPage
{
    private const string Title = "Your Rites";

    private const string Description = "The rites you voted on, Blessed or Heresy.";

    public const string BlessedVerdict = "blessed";

    public const string HeresyVerdict = "heresy";

    /// <summary>For a reader who isn't signed in: what the page keeps, and the way to sign in.</summary>
    public static string SignedOut()
        => Layout.Page(Title, Description, Section.Other, ArchiveSheet.Render(
            kicker: "Your verdicts",
            heading: Title,
            sub: Description,
            search: string.Empty,
            body: $"""
                <div class="sheet-none">
                <p>Sign in with GitHub, and every rite you bless,<br>or call heresy, is kept here.</p>
                <a class="sheet-button" href="{Html.Encode(Paths.SignIn(Paths.VotedRites))}">Sign in with GitHub</a>
                </div>
                """,
            below: string.Empty,
            left: string.Empty,
            right: string.Empty));

    /// <param name="rites">The rites voted on, newest first.</param>
    /// <param name="yours">The reader's vote on each.</param>
    /// <param name="only">The one verdict shown, or None for both.</param>
    public static string Render(IReadOnlyList<Rite> rites, IReadOnlyDictionary<int, Reaction> yours, Option<Reaction> only)
    {
        var blessed = yours.Values.Count(reaction => reaction == Reaction.Blessed);
        var heresies = yours.Count - blessed;
        var shown = rites.Where(rite => only.ForAll(verdict => yours.TryGetValue(rite.Number, out var reaction) && reaction == verdict)).ToList();
        var body = shown.Count > 0
            ? ArchiveRows.Months(shown, [], yours)
            : $"""
                <div class="sheet-none">
                <p>{(yours.Count == 0 ? "You haven’t voted on a rite yet.<br>Bless one, or call it heresy, and it’s kept here." : only.Exists(verdict => verdict == Reaction.Blessed) ? "You haven’t blessed a rite yet." : "You haven’t called a rite heresy yet.")}</p>
                <a class="sheet-button" href="{Paths.Today}">Read today’s rite</a>
                </div>
                """;
        var left = $"""
            <aside class="marginalia" aria-label="Your votes">
            {ArchiveSheet.Note("Blessed", $"""<p class="margin-number">{Html.Count(blessed)}</p>""")}
            {ArchiveSheet.Note("Heresy", $"""<p class="margin-number">{Html.Count(heresies)}</p>""")}
            </aside>
            """;
        return Layout.Page(Title, Description, Section.Other, ArchiveSheet.Render(
            kicker: "Your verdicts",
            heading: Title,
            sub: yours.Count == 0 ? Description : $"{Rites(yours.Count)} you voted on, newest first.",
            search: Verdicts(only, yours.Count, blessed, heresies),
            body: body,
            below: $"""
                <div class="voted-account">Kept for your GitHub account · <form method="post" action="{Html.Encode(Paths.SignOutFrom(Paths.VotedRites))}"><button type="submit" class="text-button">Sign out</button></form></div>
                """,
            left: left,
            right: string.Empty));
    }

    // Links, not a form: each is a page of its own, so it works without the script, and the current one says so.
    private static string Verdicts(Option<Reaction> only, int all, int blessed, int heresies)
        => $"""
            <nav class="verdicts" aria-label="Show">
            {Verdict(Paths.VotedRites, $"All {Html.Count(all)}", only.IsNone)}
            {Verdict(Paths.VotedRitesOnly(BlessedVerdict), $"Blessed {Html.Count(blessed)}", only.Exists(verdict => verdict == Reaction.Blessed))}
            {Verdict(Paths.VotedRitesOnly(HeresyVerdict), $"Heresy {Html.Count(heresies)}", only.Exists(verdict => verdict == Reaction.Heresy))}
            </nav>
            """;

    private static string Verdict(string href, string label, bool current)
        => $"""<a class="verdict" href="{Html.Encode(href)}"{(current ? " aria-current=\"page\"" : string.Empty)}>{label}</a>""";

    private static string Rites(int count) => count == 1 ? "1 rite" : $"{Html.Count(count)} rites";
}
