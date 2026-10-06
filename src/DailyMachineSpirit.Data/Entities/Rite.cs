using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

public sealed record Rite
{
    // Generous ceilings: only a runaway answer reaches them, and the generator rejects it rather than cutting it.
    public const int MaxTitleLength = 120;
    public const int MaxTextLength = 2000;
    public const int MaxHereticalTruthLength = 1000;

    /// <summary>Given when the rite is saved; the page shows it ("A RITUAL · NO. 214") and links to it.</summary>
    public int Number { get; init; }
    public DateOnly PublishedOnUtc { get; init; }
    public RiteKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    /// <summary>May contain `inline code` in backticks, like <see cref="HereticalTruth"/>.</summary>
    public string Text { get; init; } = string.Empty;
    public string HereticalTruth { get; init; } = string.Empty;
    public string GeneratedByModel { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public int BlessedCount { get; init; }
    public int HeresyCount { get; init; }
    /// <summary>For "More rites"; added after the rite is saved.</summary>
    public Option<RiteSimilarity> Similarity { get; init; }
}

public enum RiteKind
{
    Prayer,
    Ritual,
}

/// <summary>What a rite is about: rites with close <see cref="Scores"/> are related.</summary>
public sealed record RiteSimilarity
{
    /// <summary>Scores from different generator versions aren't comparable.</summary>
    public string ScoresGeneratorVersion { get; init; } = string.Empty;
    /// <summary>The rite's position along the generator's dimensions (e.g. how likely each topic and style is).</summary>
    public float[] Scores { get; init; } = [];
    public DateTime CreatedAtUtc { get; init; }
}
