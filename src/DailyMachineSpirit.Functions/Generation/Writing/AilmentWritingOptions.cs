namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// The model that writes an ailment for words written before rites had one (<c>machine-spirit.jsonc</c>,
/// <c>AilmentWriting</c>).
/// </summary>
public sealed record AilmentWritingOptions
{
    public const string SectionName = "AilmentWriting";

    public string Model { get; init; } = string.Empty;

    public Patience Patience { get; init; } = new();
}
