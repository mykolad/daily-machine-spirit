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

    /// <summary>The signed-in readers who confessed to having done the rite (#200).</summary>
    public int ConfessionCount { get; init; }

    /// <summary>Added after the rite is saved.</summary>
    public Option<RiteSimilarity> Similarity { get; init; }

    public Option<string[]> Tags { get; init; }

    /// <summary>
    /// When an Expurgator last erased words from it, None if never: a feed's readers learn from it that the entry
    /// changed (its <c>updated</c>), so they don't keep the erased words.
    /// </summary>
    public Option<DateTime> WordsErasedAtUtc { get; init; }
}
