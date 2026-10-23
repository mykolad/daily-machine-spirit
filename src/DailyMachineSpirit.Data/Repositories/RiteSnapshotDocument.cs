namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The rites' snapshot as stored: every rite as its document reads in Cosmos DB, and when it was written.</summary>
internal sealed record RiteSnapshotDocument
{
    public DateTime WrittenAtUtc { get; init; }

    public List<RiteDocument> Rites { get; init; } = [];
}
