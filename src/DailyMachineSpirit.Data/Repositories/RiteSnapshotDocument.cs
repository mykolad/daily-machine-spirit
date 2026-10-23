namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The rites' snapshot as stored: every rite as its document reads in Cosmos DB, and when it was written.</summary>
internal sealed record RiteSnapshotDocument
{
    public DateTime WrittenAtUtc { get; init; }

    public List<RiteDocument> Rites { get; init; } = [];

    /// <summary>
    /// Taken away after an erasure it couldn't follow: readers find no copy, but the blob, and so its version, stays, which
    /// a write begun before must still match.
    /// </summary>
    public bool Discarded { get; init; }
}
