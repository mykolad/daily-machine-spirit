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

    public List<PreceptDocument> Precepts { get; init; } = [];

    public List<string> Rejected { get; init; } = [];

    // Null when nothing waits to be undone, and until the first distillation: the JSON serializer doesn't know Option.
    public PreceptDocument? LastDeleted { get; init; }

    public int? LastDeletedPlace { get; init; }

    public DateTime? DistilledAtUtc { get; init; }

    public int? DistilledFrom { get; init; }

    public DateTime? NewestJudgmentReadUtc { get; init; }

    public static PreceptsDocument From(PreceptBook book) => new()
    {
        Precepts = book.Precepts.Select(PreceptDocument.From).ToList(),
        Rejected = book.Rejected,
        LastDeleted = book.LastDeleted.Map(deleted => PreceptDocument.From(deleted.Precept)).OrNull(),
        LastDeletedPlace = book.LastDeleted.Map(deleted => deleted.Place).OrNullable(),
        DistilledAtUtc = book.DistilledAtUtc.Map(Utc.From).OrNullable(),
        DistilledFrom = book.DistilledFrom.OrNullable(),
        NewestJudgmentReadUtc = book.NewestJudgmentReadUtc.Map(Utc.From).OrNullable(),
    };

    public PreceptBook ToBook() => new()
    {
        Precepts = Precepts.Select(precept => precept.ToPrecept()).ToList(),
        Rejected = Rejected,
        LastDeleted = Optional(LastDeleted).Map(deleted => new DeletedPrecept(deleted.ToPrecept(), LastDeletedPlace ?? 0)),
        DistilledAtUtc = Optional(DistilledAtUtc),
        DistilledFrom = Optional(DistilledFrom),
        NewestJudgmentReadUtc = Optional(NewestJudgmentReadUtc),
    };
}
