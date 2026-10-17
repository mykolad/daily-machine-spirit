using System.Globalization;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>A <see cref="Rite"/> as stored: its id is its day, so Cosmos itself allows one rite per day.</summary>
internal sealed record RiteDocument
{
    public const string RiteType = "rite";

    public string Id { get; init; } = string.Empty;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = RiteType;

    public int Number { get; init; }

    public DateOnly PublishedOnUtc { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    public string GeneratedByModel { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public bool Edited { get; init; }

    // The signed-in votes (VoteRepository). The anonymous counts from before sign-in were "blessedCount" and
    // "heresyCount": older documents still have them, and nothing reads them, so the counts started again from zero.
    public int BlessedVotes { get; init; }

    public int HeresyVotes { get; init; }

    // Null when missing: the JSON serializer doesn't know Option, so this is the one place a rite's null lives.
    public RiteSimilarity? Similarity { get; init; }

    // Null until its judge tagged it: rites judged before tags existed get theirs later (TagBackfill).
    public string[]? Tags { get; init; }

    public static string IdFor(DateOnly publishedOnUtc) => publishedOnUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static RiteDocument From(Rite rite, int number) => new()
    {
        Id = IdFor(rite.PublishedOnUtc),
        Number = number,
        PublishedOnUtc = rite.PublishedOnUtc,
        Kind = rite.Writing.Kind,
        Title = rite.Writing.Title,
        Text = rite.Writing.Text,
        HereticalTruth = rite.Writing.HereticalTruth,
        GeneratedByModel = rite.Writing.GeneratedByModel,
        GeneratedAtUtc = Utc.From(rite.Writing.GeneratedAtUtc),
        Edited = rite.Writing.Edited,
        BlessedVotes = rite.BlessedCount,
        HeresyVotes = rite.HeresyCount,
        Similarity = rite.Similarity.Map(Utc.From).OrNull(),
        Tags = rite.Tags.OrNull(),
    };

    public Rite ToRite() => new()
    {
        Number = Number,
        PublishedOnUtc = PublishedOnUtc,
        Writing = new RiteWriting
        {
            Kind = Kind,
            Title = Title,
            Text = Text,
            HereticalTruth = HereticalTruth,
            GeneratedByModel = GeneratedByModel,
            GeneratedAtUtc = GeneratedAtUtc,
            Edited = Edited,
        },
        BlessedCount = BlessedVotes,
        HeresyCount = HeresyVotes,
        Similarity = Optional(Similarity),
        Tags = Optional(Tags),
    };
}
