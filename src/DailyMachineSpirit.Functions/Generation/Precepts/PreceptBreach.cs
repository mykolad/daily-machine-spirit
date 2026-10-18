namespace DailyMachineSpirit.Functions.Generation.Precepts;

/// <summary>A precept that would make the writer break a rule, or is abusive, and why.</summary>
public sealed record PreceptBreach(string Precept, string Reason);
