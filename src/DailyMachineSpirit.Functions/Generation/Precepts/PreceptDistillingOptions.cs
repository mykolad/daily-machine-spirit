namespace DailyMachineSpirit.Functions.Generation.Precepts;

/// <summary>Distilling the Precepts (<c>machine-spirit.jsonc</c>, <c>PreceptDistilling</c>).</summary>
public sealed record PreceptDistillingOptions
{
    public const string SectionName = "PreceptDistilling";

    public string[] Models { get; init; } = [];

    /// <summary>How many of the newest judgments a distillation reads.</summary>
    public int JudgmentsRead { get; init; }

    public Patience Patience { get; init; } = new();
}
