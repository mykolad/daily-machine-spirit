namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The last rite number given out. Cosmos has no auto-increment, so this document hands them out.</summary>
internal sealed record NumberCounterDocument
{
    public const string CounterId = "rite-number";

    public string Id { get; init; } = CounterId;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = "counter";

    public int LastNumber { get; init; }
}
