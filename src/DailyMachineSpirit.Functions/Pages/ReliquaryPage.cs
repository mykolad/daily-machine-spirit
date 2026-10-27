using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The Reliquary (<c>/reliquary</c>): the readers' most venerated rites across the whole site (<see cref="Veneration"/>),
/// on the Archive's sheet, the most first. The Breviary is one reader's own rites; the Reliquary is everyone's. It
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
        var body = rites.Count == 0
            ? $"""
                <div class="sheet-none">
                <p>No rite is venerated yet.<br>Bless one, and it may be kept here.</p>
                <a class="sheet-button" href="{Paths.Today}">Read today’s rite</a>
                </div>
                """
            : $"""<ol class="rows">{string.Concat(rites.Select(rite => ArchiveRows.Row(rite, [], ArchiveRows.NoVotes)))}</ol>""";
        return Layout.Page(Title, Description, Section.Other, ArchiveSheet.Render(
            kicker: "The most venerated",
            heading: Title,
            sub: Html.Encode(Description),
            search: string.Empty,
            body: body,
            below: string.Empty,
            left: left,
            right: string.Empty));
    }
}
