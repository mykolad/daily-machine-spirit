namespace DailyMachineSpirit.Data.Entities;

/// <summary>What a model wrote for a rite, and which model wrote it, when: a draft's, kept as is when it's published.</summary>
public sealed record RiteWriting
{
    // Generous ceilings: only a runaway answer reaches them, and the generator rejects it rather than cutting it.
    public const int MaxTitleLength = 120;
    public const int MaxTextLength = 2000;
    public const int MaxHereticalTruthLength = 1000;

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    /// <summary>May contain `inline code` in backticks, like <see cref="HereticalTruth"/>.</summary>
    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    public string GeneratedByModel { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>
    /// A Scribe amended the words (by hand, or by asking a model to rewrite them) before they were published.
    /// <see cref="GeneratedByModel"/> still names the model that wrote the first version. Nothing says which Scribe.
    /// </summary>
    public bool Edited { get; init; }
}
