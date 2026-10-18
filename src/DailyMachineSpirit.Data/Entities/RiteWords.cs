namespace DailyMachineSpirit.Data.Entities;

/// <summary>A rite's words and where it stands, without its scores, tags or votes: a small part of each rite's document.</summary>
public sealed record RiteWords(int Number, DateOnly PublishedOnUtc, RiteKind Kind, string Title, string Text, string HereticalTruth);
