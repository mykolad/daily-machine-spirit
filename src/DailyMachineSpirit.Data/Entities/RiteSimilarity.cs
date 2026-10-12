namespace DailyMachineSpirit.Data.Entities;

/// <summary>What a rite is about: rites with close <see cref="Scores"/> are related.</summary>
public sealed record RiteSimilarity
{
    /// <summary>Scores from different generator versions aren't comparable.</summary>
    public string ScoresGeneratorVersion { get; init; } = string.Empty;

    /// <summary>The rite's position along the generator's dimensions (e.g. how likely each topic and style is).</summary>
    public float[] Scores { get; init; } = [];

    public DateTime CreatedAtUtc { get; init; }
}
