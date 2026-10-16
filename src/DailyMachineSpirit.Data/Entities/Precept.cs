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

    /// <summary>Written or edited by a Scribe, rather than as a model distilled it.</summary>
    public bool ByScribe { get; init; }
}
