using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// A signed-in reader's vote on a rite. Its id is the rite's number and the reader's voter key, so Cosmos itself allows
/// one vote per account per rite. The voter key is all that's kept about the reader.
/// </summary>
internal sealed record VoteDocument
{
    public const string VoteType = "vote";

    public string Id { get; init; } = string.Empty;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = VoteType;

    public int RiteNumber { get; init; }

    public string VoterKey { get; init; } = string.Empty;

    public Reaction Reaction { get; init; }

    public static string IdFor(int riteNumber, string voterKey) => $"vote-{riteNumber}-{voterKey}";
}
