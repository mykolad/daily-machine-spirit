namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>The model that tells whether a draft repeats a rite (<c>machine-spirit.jsonc</c>, <c>DuplicateRiteCheck</c>).</summary>
public sealed record DuplicateRiteCheckOptions
{
    public const string SectionName = "DuplicateRiteCheck";

    public string Model { get; init; } = string.Empty;

    public Patience Patience { get; init; } = new();
}
