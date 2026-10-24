using DailyMachineSpirit.Data.Entities;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>A <see cref="Draft"/> as stored, next to the rites (it becomes one in the same batch).</summary>
internal sealed record DraftDocument
{
    public const string DraftType = "draft";

    public string Id { get; init; } = string.Empty;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = DraftType;

    public DraftState State { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    public string? Ailment { get; init; }

    public string GeneratedByModel { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public bool Edited { get; init; }

    // Null when missing: the JSON serializer doesn't know Option.
    public RiteSimilarity? Similarity { get; init; }

    public string[]? Tags { get; init; }

    public Augury? Augury { get; init; }

    public DateOnly? PublishedOnUtc { get; init; }

    public DateTime? BurnedAtUtc { get; init; }

    public string? BurnedBy { get; init; }

    public int? BurnedFromPlace { get; init; }

    public string? BurnReason { get; init; }

    public string? Resembles { get; init; }

    public static string IdFor(Guid draftId) => draftId.ToString("D");

    public static DraftDocument From(Draft draft) => new()
    {
        Id = IdFor(draft.Id),
        State = draft.State,
        Kind = draft.Writing.Kind,
        Title = draft.Writing.Title,
        Text = draft.Writing.Text,
        HereticalTruth = draft.Writing.HereticalTruth,
        Ailment = draft.Writing.Ailment.OrNull(),
        GeneratedByModel = draft.Writing.GeneratedByModel,
        GeneratedAtUtc = Utc.From(draft.Writing.GeneratedAtUtc),
        Edited = draft.Writing.Edited,
        Similarity = draft.Similarity.Map(Utc.From).OrNull(),
        Tags = draft.Tags.OrNull(),
        Augury = draft.Augury.Map(augury => augury with { JudgedAtUtc = Utc.From(augury.JudgedAtUtc) }).OrNull(),
        PublishedOnUtc = draft.PublishedOnUtc.OrNullable(),
        BurnedAtUtc = draft.BurnedAtUtc.Map(Utc.From).OrNullable(),
        BurnedBy = draft.BurnedBy.Map(scribe => scribe.Value).OrNull(),
        BurnedFromPlace = draft.BurnedFromPlace.OrNullable(),
        BurnReason = draft.BurnReason.OrNull(),
        Resembles = draft.Resembles.OrNull(),
    };

    public Draft ToDraft() => new()
    {
        Id = Guid.Parse(Id),
        State = State,
        Writing = new RiteWriting
        {
            Kind = Kind,
            Title = Title,
            Text = Text,
            HereticalTruth = HereticalTruth,
            Ailment = Optional(Ailment),
            GeneratedByModel = GeneratedByModel,
            GeneratedAtUtc = GeneratedAtUtc,
            Edited = Edited,
        },
        Similarity = Optional(Similarity),
        Tags = Optional(Tags),
        Augury = Optional(Augury),
        PublishedOnUtc = Optional(PublishedOnUtc),
        BurnedAtUtc = Optional(BurnedAtUtc),
        BurnedBy = Optional(BurnedBy).Map(id => new ScribeId(id)),
        BurnedFromPlace = Optional(BurnedFromPlace),
        BurnReason = Optional(BurnReason),
        Resembles = Optional(Resembles),
    };
}
