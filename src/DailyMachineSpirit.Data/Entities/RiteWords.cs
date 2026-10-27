namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// What a search reads of a rite, and where it stands: its words and its subjects' tags, without its scores or votes. A
/// small part of each rite's document.
/// </summary>
public sealed record RiteWords(int Number, DateOnly PublishedOnUtc, RiteKind Kind, string Title, string Text, string HereticalTruth, IReadOnlyList<string> Tags);
