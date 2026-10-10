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

    public string GeneratedByModel { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    // Null when missing: the JSON serializer doesn't know Option.
    public RiteSimilarity? Similarity { get; init; }

    public Augury? Augury { get; init; }

    public DateOnly? PublishedOnUtc { get; init; }

    public static string IdFor(Guid draftId) => draftId.ToString("D");

    public static DraftDocument From(Draft draft) => new()
    {
        Id = IdFor(draft.Id),
        State = draft.State,
        Kind = draft.Kind,
        Title = draft.Title,
        Text = draft.Text,
        HereticalTruth = draft.HereticalTruth,
        GeneratedByModel = draft.GeneratedByModel,
        GeneratedAtUtc = Utc.From(draft.GeneratedAtUtc),
        Similarity = draft.Similarity.Map(Utc.From).OrNull(),
        Augury = draft.Augury.Map(augury => augury with { JudgedAtUtc = Utc.From(augury.JudgedAtUtc) }).OrNull(),
        PublishedOnUtc = draft.PublishedOnUtc.OrNullable(),
    };

    public Draft ToDraft() => new()
    {
        Id = Guid.Parse(Id),
        State = State,
        Kind = Kind,
        Title = Title,
        Text = Text,
        HereticalTruth = HereticalTruth,
        GeneratedByModel = GeneratedByModel,
        GeneratedAtUtc = GeneratedAtUtc,
        Similarity = Optional(Similarity),
        Augury = Optional(Augury),
        PublishedOnUtc = Optional(PublishedOnUtc),
    };
}
