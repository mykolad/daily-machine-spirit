namespace DailyMachineSpirit.Functions.Caching;

/// <summary>Purging the pages a vote changed (<c>machine-spirit.json</c>, <c>PagePurging</c>).</summary>
public sealed record PagePurgingOptions
{
    public const string SectionName = "PagePurging";

    /// <summary>At most one purge per this, per instance: the votes in between share it.</summary>
    public TimeSpan BatchEvery { get; init; }
}
