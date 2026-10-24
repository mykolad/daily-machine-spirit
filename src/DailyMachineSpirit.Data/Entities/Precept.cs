using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// One rule the Machine Spirit learned from the Scribes' judgments (#12), e.g. "Prefer rites whose Heretical Truth
/// names the real fix". The writer follows the Precepts. A pinned precept stays when they're distilled again; the
/// others make way for the new ones.
/// </summary>
public sealed record Precept
{
    public const int MaxLength = 300;

    public Guid Id { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool Pinned { get; init; }

    public PreceptOrigin Origin { get; init; }

    /// <summary>
    /// The Scribe (their Access id) who wrote or amended it, or who pinned a distilled one; None for one the Machine
    /// Spirit's alone, or kept before it was known.
    /// </summary>
    public Option<ScribeId> By { get; init; }

    /// <summary>Written or edited by a Scribe, rather than as a model distilled it.</summary>
    public bool ByScribe => Origin != PreceptOrigin.Distilled;
}
