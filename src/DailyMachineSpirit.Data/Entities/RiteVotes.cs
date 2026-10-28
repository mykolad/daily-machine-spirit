namespace DailyMachineSpirit.Data.Entities;

/// <summary>A rite's vote counts, without its words: what ranks it among the most venerated.</summary>
public sealed record RiteVotes(int Number, int Blessed, int Heresy);
