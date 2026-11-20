using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>What a model wrote for a rite, and which model wrote it, when: a draft's, kept as is when it's published.</summary>
public sealed record RiteWriting
{
    // Generous ceilings: only a runaway answer reaches them, and the generator rejects it rather than cutting it.
    public const int MaxTitleLength = 120;
    public const int MaxTextLength = 2000;
    public const int MaxHereticalTruthLength = 1000;
    public const int MaxAilmentLength = 300;

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    /// <summary>May contain `inline code` in backticks, like <see cref="HereticalTruth"/>.</summary>
    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    /// <summary>
    /// What the reader tells the real truth from (#199); None for a rite written before them, which keeps the plain seal
    /// until it's given some.
    /// </summary>
    public Option<FalseTruths> FalseTruths { get; init; }

    /// <summary>
    /// The trouble the rite is for, in a line, without its fix ("a test that fails at random and passes when re-run"): what
    /// a client of the rites API matches a user's trouble on.
    /// </summary>
    public Option<string> Ailment { get; init; }

    /// <summary>
    /// The form the writer was asked for (a litany, a vigil, a penance: <c>RiteForms</c> in the Functions app), by its key;
    /// None for a rite written before forms took turns (#193), whose form its title tells.
    /// </summary>
    public Option<string> Form { get; init; }

    public string GeneratedByModel { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>
    /// A Scribe amended the words (by hand, or by asking a model to rewrite them) before they were published.
    /// <see cref="GeneratedByModel"/> still names the model that wrote the first version. Nothing says which Scribe.
    /// </summary>
    public bool Edited { get; init; }
}
