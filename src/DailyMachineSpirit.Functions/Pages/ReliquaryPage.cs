using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Reliquary (<c>/reliquary</c>): the readers' most venerated rites across the whole site (<see cref="Veneration"/>),
/// the most first. The Archive in its other order, so it has the Archive's masthead and sheet, and both offer the two
/// orders (<see cref="ArchiveSheet.Orders"/>). The Breviary is one reader's own rites; the Reliquary is everyone's. It
/// changes with every vote, so it's purged with the pages that show a rite's counts (<see cref="Paths.ShowingVotesOf"/>).
/// </summary>
public static class ReliquaryPage
{
    private const string Title = "The Reliquary";

    private const string Description = "The rites the readers venerate most: blessed most, less those who called them heresy.";

    /// <param name="rites">The most venerated rites, the most first.</param>
    public static string Render(IReadOnlyList<Rite> rites, DateTimeOffset now)
    {
        var left = $"""
            <aside class="marginalia" aria-label="About the Reliquary">
            {ArchiveSheet.Countdown(now)}
            </aside>
            """;
        // The rows' order says each one's place, so they wear no relic badge.
        var body = rites.Count == 0
            ? $"""
                <div class="sheet-none">
                <p>No rite is venerated yet.<br>Bless one, and it may be kept here.</p>
                <a class="sheet-button" href="{Paths.Today}">Read today’s rite</a>
                </div>
                """
            : $"""<ol class="rows">{string.Concat(rites.Select(rite => ArchiveRows.Row(rite, [], ArchiveRows.NoVotes, Veneration.NoPlaces)))}</ol>""";
        return Layout.Page(Title, Description, Section.Reliquary, $"""
            {Masthead.Render(isHeading: false)}
            {ArchiveSheet.Render(
                kicker: string.Empty,
                heading: Title,
                sub: Html.Encode(Description),
                search: ArchiveSheet.Orders(byVeneration: true),
                body: body,
                below: string.Empty,
                left: left,
                right: string.Empty)}
            """);
    }
}
