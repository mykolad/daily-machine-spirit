namespace DailyMachineSpirit.Functions.Pages;

/// <summary>The orders the Archive's rites are offered in (<see cref="ArchiveSheet.Orders"/>), each a page of its own.</summary>
public enum ArchiveOrder
{
    /// <summary>The Archive itself.</summary>
    Newest,

    /// <summary>The Reliquary.</summary>
    MostVenerated,

    /// <summary>The Reliquary's other order (#200).</summary>
    MostConfessed,
}
