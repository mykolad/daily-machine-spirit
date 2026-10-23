using System.Text.Json.Serialization;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>What a rite or a draft without an ailment is read as.</summary>
internal sealed record WordsWithoutAilmentDocument
{
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("_etag")]
    public string ETag { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    public WordsWithoutAilment ToWords() => new(Id, ETag, Type == RiteDocument.RiteType, Kind, Title, Text, HereticalTruth);
}
