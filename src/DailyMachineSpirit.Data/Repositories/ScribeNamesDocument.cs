using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>Every Scribe's name in one document, so its ETag keeps two Scribes from taking the same name at once.</summary>
internal sealed record ScribeNamesDocument
{
    public const string ScribeNamesId = "scribe-names";

    public string Id { get; init; } = ScribeNamesId;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = "scribe-names";

    public List<ScribeName> Names { get; init; } = [];
}
