namespace DailyMachineSpirit.Data.Entities;

public class Rite
{
    // Generous ceilings: only a runaway answer reaches them, and the generator rejects it rather than cutting it.
    public const int MaxTitleLength = 120;
    public const int MaxTextLength = 2000;
    public const int MaxHereticalTruthLength = 1000;

    /// <summary>Given when the rite is saved; the page shows it ("A RITUAL · NO. 214") and links to it.</summary>
    public int Number { get; set; }
    public DateOnly PublishedOnUtc { get; set; }
    public RiteKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>May contain `inline code` in backticks, like <see cref="HereticalTruth"/>.</summary>
    public string Text { get; set; } = string.Empty;
    public string HereticalTruth { get; set; } = string.Empty;
    public string GeneratedByModel { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public int BlessedCount { get; set; }
    public int HeresyCount { get; set; }
    /// <summary>For "More rites"; added after the rite is saved, so it can be missing.</summary>
    public RiteSimilarity? Similarity { get; set; }
}

public enum RiteKind
{
    Prayer,
    Ritual,
}

/// <summary>What a rite is about: rites with close <see cref="Scores"/> are related.</summary>
public class RiteSimilarity
{
    /// <summary>Scores from different generator versions aren't comparable.</summary>
    public string ScoresGeneratorVersion { get; set; } = string.Empty;
    /// <summary>The rite's position along the generator's dimensions (e.g. how likely each topic and style is).</summary>
    public float[] Scores { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}
