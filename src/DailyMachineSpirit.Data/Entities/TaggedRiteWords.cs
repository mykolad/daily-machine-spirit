namespace DailyMachineSpirit.Data.Entities;

/// <summary>A rite's words, where it stands and its tags' keys, without its scores or votes.</summary>
public sealed record TaggedRiteWords(
    int Number, DateOnly PublishedOnUtc, RiteKind Kind, string Title, string Text, string HereticalTruth, IReadOnlyList<string> Tags);
