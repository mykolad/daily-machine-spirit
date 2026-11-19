using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The rites a signed-in reader has witnessed (#215), one document per reader. Its id is the reader's voter key, as a
/// vote's is, and that key is all that's kept about them.
/// </summary>
internal sealed record WitnessedDocument
{
    public const string WitnessedType = "witnessed";

    public string Id { get; init; } = string.Empty;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = WitnessedType;

    public string VoterKey { get; init; } = string.Empty;

    /// <summary>The rites' numbers, lowest first.</summary>
    public List<int> Rites { get; init; } = [];

    public static string IdFor(VoterKey voterKey) => $"witnessed-{voterKey.Value}";
}
