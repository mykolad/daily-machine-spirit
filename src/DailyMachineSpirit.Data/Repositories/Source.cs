namespace DailyMachineSpirit.Data.Repositories;

/// <summary>Where <see cref="RitesWithFallback"/> sends a read.</summary>
internal enum Source
{
    /// <summary>Cosmos DB, which hasn't failed lately.</summary>
    Cosmos,

    /// <summary>The snapshot, while Cosmos DB failed within the minute or another read is asking it again.</summary>
    Copy,

    /// <summary>Cosmos DB again, the one read per instance that asks whether it's back.</summary>
    Probe,
}
