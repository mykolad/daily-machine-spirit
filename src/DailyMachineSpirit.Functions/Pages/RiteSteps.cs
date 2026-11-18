using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The links to the rite before and after one (#195), under its card, so the rites read one after another, as a daily
/// comic's back pages do. Plain links, which the script also follows on the arrow keys and a swipe.
/// </summary>
public static class RiteSteps
{
    /// <summary>The two links, or only the one there is; nothing for the site's only rite.</summary>
    public static string Render(RiteNeighbours neighbours)
        => neighbours.Previous.IsNone && neighbours.Next.IsNone
            ? string.Empty
            : $"""
                <nav class="rite-steps" aria-label="The rites before and after">
                {Step(neighbours.Previous.Map(rite => (rite, Paths.Rite(rite.Number))), "prev", "rite-step", "Previous rite")}
                {Step(neighbours.Next.Map(rite => (rite, NextPath(rite, neighbours))), "next", "rite-step rite-step-next", "Next rite")}
                </nav>
                """;

    /// <summary>The page's listing, its head naming the rites before and after (<c>rel="prev"</c>, <c>rel="next"</c>).</summary>
    public static SearchListing Listed(SearchListing listing, RiteNeighbours neighbours)
        => listing with { Previous = neighbours.Previous.Map(rite => Paths.Rite(rite.Number)), Next = neighbours.Next.Map(rite => NextPath(rite, neighbours)) };

    // The arrow points the way it goes, before the number on the left and after it on the right. A screen reader hears
    // which way instead, and the number without "NO.", as in the Archive's rows.
    private static string Step(Option<(RiteHeading Rite, string Path)> rite, string rel, string classes, string which)
        => rite.Match(
            Some: step =>
            {
                var number = rel == "prev"
                    ? $"""<span aria-hidden="true">← NO. </span>{step.Rite.Number}"""
                    : $"""<span aria-hidden="true">NO. </span>{step.Rite.Number}<span aria-hidden="true"> →</span>""";
                return $"""<a class="{classes}" href="{step.Path}" rel="{rel}"><span class="sr-only">{which}, </span><span class="rite-step-number">{number}</span><span class="rite-step-title">{Html.Encode(step.Rite.Title)}</span></a>""";
            },
            None: () => string.Empty);

    // The newest rite is Today's, so the step to it leads to Today: left from Today, then right, comes back to Today
    // rather than to the same rite on a page of its own.
    private static string NextPath(RiteHeading next, RiteNeighbours neighbours)
        => neighbours.NextIsNewest ? Paths.Today : Paths.Rite(next.Number);
}
