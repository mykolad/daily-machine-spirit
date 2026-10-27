using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>What the search index reads of each rite.</summary>
internal sealed record RiteWordsDocument
{
    public int Number { get; init; }

    public DateOnly PublishedOnUtc { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    // Null for a rite judged before tags existed, or never judged: the JSON serializer doesn't know Option.
    public string[]? Tags { get; init; }

    public RiteWords ToRiteWords() => new(Number, PublishedOnUtc, Kind, Title, Text, HereticalTruth, Tags ?? []);
}
