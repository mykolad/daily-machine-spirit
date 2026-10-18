using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>What a search reads of each rite it finds.</summary>
internal sealed record FoundRiteDocument
{
    public int Number { get; init; }

    public DateOnly PublishedOnUtc { get; init; }

    // Null for a rite judged before tags existed, or never judged: the JSON serializer doesn't know Option.
    public string[]? Tags { get; init; }

    public FoundRite ToFoundRite() => new(Number, PublishedOnUtc, Tags ?? []);
}
