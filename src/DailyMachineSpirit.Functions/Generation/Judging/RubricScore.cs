using System.ComponentModel;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>A model's judgment of a draft against the rubric: its JSON schema is made from this.</summary>
public sealed record RubricScore(
    [property: Description("How good the rite is to publish, from 1 (flat, wrong or pointless) to 10 (excellent).")] int Score,
    [property: Description("One sentence on why.")] string Reason);
