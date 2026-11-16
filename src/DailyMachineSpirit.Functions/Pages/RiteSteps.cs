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
                {Step(neighbours.Previous, "prev", "rite-step", "Previous rite")}
                {Step(neighbours.Next, "next", "rite-step rite-step-next", "Next rite")}
                </nav>
                """;

    /// <summary>The page's listing, its head naming the rites before and after (<c>rel="prev"</c>, <c>rel="next"</c>).</summary>
    public static SearchListing Listed(SearchListing listing, RiteNeighbours neighbours)
        => listing with { Previous = neighbours.Previous.Map(rite => Paths.Rite(rite.Number)), Next = neighbours.Next.Map(rite => Paths.Rite(rite.Number)) };

    // The arrow points the way it goes, before the number on the left and after it on the right. A screen reader hears
    // which way instead, and the number without "NO.", as in the Archive's rows.
    private static string Step(Option<RiteHeading> rite, string rel, string classes, string which)
        => rite.Match(
            Some: step =>
            {
                var number = rel == "prev"
                    ? $"""<span aria-hidden="true">← NO. </span>{step.Number}"""
                    : $"""<span aria-hidden="true">NO. </span>{step.Number}<span aria-hidden="true"> →</span>""";
                return $"""<a class="{classes}" href="{Paths.Rite(step.Number)}" rel="{rel}"><span class="sr-only">{which}, </span><span class="rite-step-number">{number}</span><span class="rite-step-title">{Html.Encode(step.Title)}</span></a>""";
            },
            None: () => string.Empty);
}
