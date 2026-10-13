using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>Earlier rites, newest first, a page at a time.</summary>
public static class ArchivePage
{
    public const int PageSize = 6;

    /// <param name="olderThan">Where the next page starts, or None when these are the oldest.</param>
    public static string Render(IReadOnlyList<Rite> rites, Option<int> olderThan)
        => Layout.Page("The Archive", "Earlier litanies, newest first.", Section.Archive, $"""
            <h1 class="archive-title">The Archive</h1>
            <p class="archive-subtitle">Earlier litanies, newest first.</p>
            <div class="archive-divider">{Svg.BrassDivider}</div>
            {RiteRows.Render(rites, olderThan.Map(Paths.ArchiveBefore), "The archive ends here. Earlier knowledge is lost.")}
            """);
}
