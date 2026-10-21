using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>What the rites' words and tags are read as, from each rite's document.</summary>
internal sealed record TaggedRiteWordsDocument
{
    public int Number { get; init; }

    public DateOnly PublishedOnUtc { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    // Null for a rite judged before tags existed, or never judged: the JSON serializer doesn't know Option.
    public string[]? Tags { get; init; }

    public TaggedRiteWords ToTaggedRiteWords() => new(Number, PublishedOnUtc, Kind, Title, Text, HereticalTruth, Tags ?? []);
}
