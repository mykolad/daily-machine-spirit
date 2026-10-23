using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A rite as it stands before its Heretical Truth is revealed: its words, the trouble it's for, where it stands and its
/// tags' keys, without the truth, its scores or votes.
/// </summary>
public sealed record UnrevealedRite(
    int Number, DateOnly PublishedOnUtc, RiteKind Kind, string Title, string Text, Option<string> Ailment, IReadOnlyList<string> Tags);
