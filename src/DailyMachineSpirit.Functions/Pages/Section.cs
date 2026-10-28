namespace DailyMachineSpirit.Functions.Pages;

/// <summary>Which navigation link is the current page, if any.</summary>
public enum Section
{
    Today,
    Archive,
    About,
    Breviary,

    /// <summary>The Archive in its other order: the Archive's link is current, but its search is the nav's own.</summary>
    Reliquary,
    Other,
}
