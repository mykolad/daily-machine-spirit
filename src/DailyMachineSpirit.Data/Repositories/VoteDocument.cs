using DailyMachineSpirit.Data.Entities;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// A signed-in reader's vote on a rite: their verdict and their confession (#200), kept while they have either. Its id is
/// the rite's number and the reader's voter key, so Cosmos itself allows one per account per rite. The voter key is all
/// that's kept about the reader.
/// </summary>
internal sealed record VoteDocument
{
    public const string VoteType = "vote";

    public string Id { get; init; } = string.Empty;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = VoteType;

    public int RiteNumber { get; init; }

    public string VoterKey { get; init; } = string.Empty;

    // Null for a reader who only confessed: the JSON serializer doesn't know Option.
    public Reaction? Reaction { get; init; }

    // Missing on the votes from before confessions, which read as false.
    public bool Confessed { get; init; }

    public static string IdFor(int riteNumber, VoterKey voterKey) => $"vote-{riteNumber}-{voterKey.Value}";

    public ReaderVote ToReaderVote() => new(Optional(Reaction), Confessed);

    public VoteDocument With(ReaderVote vote) => this with { Reaction = vote.Reaction.OrNullable(), Confessed = vote.Confessed };
}
