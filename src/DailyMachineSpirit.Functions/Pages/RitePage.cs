using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A rite's own page (<c>/r/&lt;number&gt;</c>): the link that's shared, and where the archive leads.</summary>
public static class RitePage
{
    /// <param name="more">Rites to read next; the section is left out when there are none (the site's first rite).</param>
    /// <param name="places">The places of the rites the Reliquary keeps, for the rite's badge.</param>
    /// <param name="neighbours">The rites before and after it, to read on to (#195).</param>
    /// <param name="listing">The rite's address; the page adds what search engines read about the rite itself.</param>
    public static string Render(
        Rite rite, IReadOnlyList<Rite> more, IReadOnlyDictionary<int, int> places, DateTimeOffset countedAt, RiteNeighbours neighbours, SearchListing listing)
        => Layout.Page(rite.Writing.Title, Html.FirstLine(rite.Writing.Text.Replace("`", string.Empty)), AboutTheRite(rite, RiteSteps.Listed(listing, neighbours)), Section.Other, $"""
            <a class="back" href="{Paths.Archive}">{Svg.BackIcon}Back to the Archive</a>
            {RiteCard.Render(rite, ownPage: true, countedAt, marginNotes: string.Empty, places)}
            {RiteSteps.Render(neighbours)}
            {MoreRites(more)}
            """);

    private static SearchListing AboutTheRite(Rite rite, SearchListing listing)
        => listing with
        {
            OpenGraphType = "article",
            StructuredData = listing.Canonical.Map(url => RiteStructuredData.Json(rite, url)),
            // Its own card, its alt text the joke: the title and the text, the code without its backticks.
            Image = new ShareImage(Paths.RiteCard(rite.Number), $"{rite.Writing.Title}. {ShareCards.CardText.Plain(rite.Writing.Text)}"),
        };

    // The same small parchments as Today's recent rites.
    private static string MoreRites(IReadOnlyList<Rite> more)
        => more.Count == 0
            ? string.Empty
            : $"""
                <section class="shelf" aria-labelledby="more-rites">
                <h2 id="more-rites">More rites</h2>
                {ParchmentCards.List(more)}
                </section>
                """;
}
