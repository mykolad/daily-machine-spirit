using DailyMachineSpirit.Data.Entities;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The Scribes' order: the drafts they placed, first to be published first. The Augury orders the rest.</summary>
internal sealed record PlacementsDocument
{
    public const string PlacementsId = "placements";

    public string Id { get; init; } = PlacementsId;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = "placements";

    public List<Guid> Order { get; init; } = [];
}

/// <summary>A <see cref="ScribeDecision"/> as stored.</summary>
internal sealed record ScribeDecisionDocument
{
    public const string DecisionType = "decision";

    public string Id { get; init; } = string.Empty;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = DecisionType;

    public Guid DraftId { get; init; }

    public ScribeAction Action { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    // Null when missing: the JSON serializer doesn't know Option.
    public int? CalendarPlace { get; init; }

    public int? AuguryPlace { get; init; }

    public float? AuguryQuality { get; init; }

    public string? Note { get; init; }

    public DateTime DecidedAtUtc { get; init; }

    public static ScribeDecisionDocument From(ScribeDecision decision) => new()
    {
        Id = decision.Id.ToString("D"),
        DraftId = decision.DraftId,
        Action = decision.Action,
        Kind = decision.Kind,
        Title = decision.Title,
        Text = decision.Text,
        HereticalTruth = decision.HereticalTruth,
        CalendarPlace = decision.CalendarPlace.OrNullable(),
        AuguryPlace = decision.AuguryPlace.OrNullable(),
        AuguryQuality = decision.AuguryQuality.OrNullable(),
        Note = decision.Note.OrNull(),
        DecidedAtUtc = Utc.From(decision.DecidedAtUtc),
    };

    public ScribeDecision ToDecision() => new()
    {
        Id = Guid.Parse(Id),
        DraftId = DraftId,
        Action = Action,
        Kind = Kind,
        Title = Title,
        Text = Text,
        HereticalTruth = HereticalTruth,
        CalendarPlace = Optional(CalendarPlace),
        AuguryPlace = Optional(AuguryPlace),
        AuguryQuality = Optional(AuguryQuality),
        Note = Optional(Note),
        DecidedAtUtc = DecidedAtUtc,
    };
}
