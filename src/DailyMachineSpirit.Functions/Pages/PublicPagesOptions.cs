namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The readers' pages (<c>machine-spirit.json</c>, <c>PublicPages</c>).</summary>
public sealed record PublicPagesOptions
{
    public const string SectionName = "PublicPages";

    public int ArchivePageSize { get; init; }

    public int RecentRitesOnToday { get; init; }

    public int MoreRitesOnARite { get; init; }
}
