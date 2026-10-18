namespace DailyMachineSpirit.Functions.Generation.Precepts;

/// <summary>Checking precepts against the writer's rules (<c>machine-spirit.jsonc</c>, <c>PreceptCheck</c>).</summary>
public sealed record PreceptCheckOptions
{
    public const string SectionName = "PreceptCheck";

    /// <summary>Tried in order within the patience's time in all, so another answers when one fails.</summary>
    public string[] Models { get; init; } = [];

    public Patience Patience { get; init; } = new();
}
