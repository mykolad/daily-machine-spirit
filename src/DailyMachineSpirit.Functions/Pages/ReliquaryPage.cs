using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Reliquary (<c>/reliquary</c>): the readers' most venerated rites across the whole site (<see cref="Veneration"/>),
/// the most first; and in its other order (<c>/reliquary/confessed</c>, #200) the rites they most confess to
/// (<see cref="Confession"/>). The Archive in its other orders, so it has the Archive's masthead and sheet, and each
/// offers all three (<see cref="ArchiveSheet.Orders"/>). The Breviary is one reader's own rites; the Reliquary is
/// everyone's. It changes with every vote, so it's purged with the pages that show a rite's counts
/// (<see cref="Paths.ShowingVotesOf"/>).
/// </summary>
public static class ReliquaryPage
{
    private const string Title = "The Reliquary";

    private const string VeneratedDescription = "The rites the readers venerate most: blessed most, less those who called them heresy.";

    private const string ConfessedDescription = "The rites the faithful most confess to.";

    /// <param name="rites">The rites kept in that order, the first first.</param>
    /// <param name="order">The most venerated or the most confessed.</param>
    public static string Render(IReadOnlyList<Rite> rites, ArchiveOrder order, DateTimeOffset now, SearchListing listing)
    {
        var confessed = order == ArchiveOrder.MostConfessed;
        var description = confessed ? ConfessedDescription : VeneratedDescription;
        var left = $"""
            <aside class="marginalia" aria-label="About the Reliquary">
            {ArchiveSheet.Countdown(now)}
            </aside>
            """;
        // The rows' order says each one's place, so they wear no relic badge.
        var body = rites.Count == 0
            ? $"""
                <div class="sheet-none">
                <p>{(confessed ? "No rite is confessed to yet.<br>Confess to one, and it may be kept here." : "No rite is venerated yet.<br>Bless one, and it may be kept here.")}</p>
                <a class="sheet-button" href="{Paths.Today}">Read today’s rite</a>
                </div>
                """
            : $"""<ol class="rows">{string.Concat(rites.Select(rite => confessed ? ArchiveRows.ConfessedRow(rite) : ArchiveRows.Row(rite, [], ArchiveRows.NoVotes, RelicStandings.None)))}</ol>""";
        return Layout.Page(Title, description, listing, Section.Reliquary, $"""
            {Masthead.Render(isHeading: false)}
            {ArchiveSheet.Render(
                above: string.Empty,
                kicker: string.Empty,
                heading: Title,
                sub: Html.Encode(description),
                search: ArchiveSheet.Orders(order),
                body: body,
                below: string.Empty,
                left: left,
                right: string.Empty)}
            """);
    }
}
