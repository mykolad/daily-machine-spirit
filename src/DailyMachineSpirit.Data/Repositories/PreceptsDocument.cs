using DailyMachineSpirit.Data.Entities;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The <see cref="PreceptBook"/> as stored: one document, so its ETag guards every change to the Precepts.</summary>
internal sealed record PreceptsDocument
{
    public const string PreceptsId = "precepts";

    public string Id { get; init; } = PreceptsId;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = "precepts";

    public List<Precept> Precepts { get; init; } = [];

    public List<string> Rejected { get; init; } = [];

    // Null until the first distillation: the JSON serializer doesn't know Option.
    public DateTime? DistilledAtUtc { get; init; }

    public int? DistilledFrom { get; init; }

    public static PreceptsDocument From(PreceptBook book) => new()
    {
        Precepts = book.Precepts,
        Rejected = book.Rejected,
        DistilledAtUtc = book.DistilledAtUtc.Map(Utc.From).OrNullable(),
        DistilledFrom = book.DistilledFrom.OrNullable(),
    };

    public PreceptBook ToBook() => new()
    {
        Precepts = Precepts,
        Rejected = Rejected,
        DistilledAtUtc = Optional(DistilledAtUtc),
        DistilledFrom = Optional(DistilledFrom),
    };
}
