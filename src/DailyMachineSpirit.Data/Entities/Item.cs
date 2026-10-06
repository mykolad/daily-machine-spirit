namespace DailyMachineSpirit.Data.Entities;

/// <summary>The day's prayer or ritual, written by an LLM with its Heretical Truth.</summary>
public class Item
{
    // Generous ceilings: only a runaway answer reaches them, and the generator rejects it rather than cutting it.
    public const int MaxTitleLength = 120;
    public const int MaxTextLength = 2000;
    public const int MaxHereticalTruthLength = 1000;
    public const int MaxModelLength = 100;

    /// <summary>Also the number the page shows ("A RITUAL · NO. 214").</summary>
    public int Id { get; set; }
    public DateOnly PublishedOnUtc { get; set; }
    public ItemKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>May contain `inline code` in backticks, like <see cref="HereticalTruth"/>.</summary>
    public string Text { get; set; } = string.Empty;
    public string HereticalTruth { get; set; } = string.Empty;
    public string GeneratedByModel { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public int BlessedCount { get; set; }
    public int HeresyCount { get; set; }
}

public enum ItemKind
{
    Prayer,
    Ritual,
}
