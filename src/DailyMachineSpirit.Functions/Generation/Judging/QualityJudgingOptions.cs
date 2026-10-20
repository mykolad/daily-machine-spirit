namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>How the Augury's quality is judged (<c>machine-spirit.jsonc</c>, <c>QualityJudging</c>; #10).</summary>
public sealed record QualityJudgingOptions
{
    public const string SectionName = "QualityJudging";

    /// <summary>The judges: a draft is judged by the first that didn't write it, so no model judges its own writing.</summary>
    public string[] Models { get; init; } = [];

    /// <summary>A model's score for the same draft varies from one ask to the next, so the quality averages a few.</summary>
    public int ScoresPerDraft { get; init; }

    public Patience Patience { get; init; } = new();
}
