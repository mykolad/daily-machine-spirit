using System.Globalization;
using DailyMachineSpirit.Data.Entities;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>A <see cref="Rite"/> as stored: its id is its day, so Cosmos itself allows one rite per day.</summary>
internal sealed record RiteDocument
{
    public const string RiteType = "rite";

    /// <summary>Where an erasure's patch writes its time (<see cref="Rite.WordsErasedAtUtc"/>).</summary>
    public const string WordsErasedAtUtcField = "/wordsErasedAtUtc";

    public string Id { get; init; } = string.Empty;

    public string Partition { get; init; } = CosmosCalls.SharedPartition;

    public string Type { get; init; } = RiteType;

    public int Number { get; init; }

    public DateOnly PublishedOnUtc { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    // Null for a rite written before them (#199).
    public FalseTruths? FalseTruths { get; init; }

    // Written for a rite from before them (#199), for a Scribe to accept or have written again; null once accepted.
    public FalseTruths? ProposedFalseTruths { get; init; }

    public string? Ailment { get; init; }

    public string? Form { get; init; }

    public string GeneratedByModel { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public bool Edited { get; init; }

    // The signed-in votes (VoteRepository). The anonymous counts from before sign-in were "blessedCount" and
    // "heresyCount": older documents still have them, and nothing reads them, so the counts started again from zero.
    public int BlessedVotes { get; init; }

    public int HeresyVotes { get; init; }

    // Null when missing: the JSON serializer doesn't know Option, so this is the one place a rite's null lives.
    public RiteSimilarity? Similarity { get; init; }

    // Null until its judge tagged it.
    public string[]? Tags { get; init; }

    public DateTime? WordsErasedAtUtc { get; init; }

    public static string IdFor(DateOnly publishedOnUtc) => publishedOnUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Whether a document's id is a rite's: its day, as no other document's id is (a draft's is a Guid).</summary>
    public static bool IsRiteId(string id) => DateOnly.TryParseExact(id, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    public static RiteDocument From(Rite rite, int number) => new()
    {
        Id = IdFor(rite.PublishedOnUtc),
        Number = number,
        PublishedOnUtc = rite.PublishedOnUtc,
        Kind = rite.Writing.Kind,
        Title = rite.Writing.Title,
        Text = rite.Writing.Text,
        HereticalTruth = rite.Writing.HereticalTruth,
        FalseTruths = rite.Writing.FalseTruths.OrNull(),
        Ailment = rite.Writing.Ailment.OrNull(),
        Form = rite.Writing.Form.OrNull(),
        GeneratedByModel = rite.Writing.GeneratedByModel,
        GeneratedAtUtc = Utc.From(rite.Writing.GeneratedAtUtc),
        Edited = rite.Writing.Edited,
        BlessedVotes = rite.BlessedCount,
        HeresyVotes = rite.HeresyCount,
        Similarity = rite.Similarity.Map(Utc.From).OrNull(),
        Tags = rite.Tags.OrNull(),
        WordsErasedAtUtc = rite.WordsErasedAtUtc.Map(Utc.From).OrNullable(),
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
            FalseTruths = Optional(FalseTruths),
            Ailment = Optional(Ailment),
            Form = Optional(Form),
            GeneratedByModel = GeneratedByModel,
            GeneratedAtUtc = GeneratedAtUtc,
            Edited = Edited,
        },
        BlessedCount = BlessedVotes,
        HeresyCount = HeresyVotes,
        Similarity = Optional(Similarity),
        Tags = Optional(Tags),
        WordsErasedAtUtc = Optional(WordsErasedAtUtc).Map(Utc.From),
    };
}
