namespace DailyMachineSpirit.Data.Entities;

/// <summary>What an item is about, for "More rites": items with close <see cref="Scores"/> are related.</summary>
public class ItemProfile
{
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    /// <summary>Scores from different generator versions aren't comparable.</summary>
    public string ScoresGeneratorVersion { get; set; } = string.Empty;
    /// <summary>The item's position along the generator's dimensions (e.g. how likely each topic and style is).</summary>
    public float[] Scores { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}
