using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

public sealed record Rite
{
    /// <summary>Given when the rite is saved: the next in sequence.</summary>
    public int Number { get; init; }

    public DateOnly PublishedOnUtc { get; init; }

    public RiteWriting Writing { get; init; } = new();

    public int BlessedCount { get; init; }

    public int HeresyCount { get; init; }

    /// <summary>Added after the rite is saved.</summary>
    public Option<RiteSimilarity> Similarity { get; init; }

    public Option<string[]> Tags { get; init; }
}
