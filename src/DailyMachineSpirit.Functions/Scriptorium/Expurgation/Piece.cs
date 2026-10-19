namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>A run of a rite's text: a word an Expurgator can erase, or what lies between words.</summary>
public sealed record Piece(string Text, bool IsWord);
