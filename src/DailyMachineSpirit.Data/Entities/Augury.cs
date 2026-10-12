namespace DailyMachineSpirit.Data.Entities;

/// <summary>How good a judge thinks a draft is: the Augury publishes the better ones first.</summary>
public sealed record Augury
{
    /// <summary>From 0 (weak) to 1 (excellent).</summary>
    public float Quality { get; init; }

    /// <summary>Which judge, and which version of its question: qualities from different judges aren't comparable.</summary>
    public string JudgedBy { get; init; } = string.Empty;

    public DateTime JudgedAtUtc { get; init; }
}
