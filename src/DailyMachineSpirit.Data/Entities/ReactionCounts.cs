namespace DailyMachineSpirit.Data.Entities;

/// <summary>A rite's reactions so far: its two verdicts, and the readers who confessed to it (#200).</summary>
public sealed record ReactionCounts(int Blessed, int Heresy, int Confessions);
