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

    public string Partition { get; init; } = RiteRepository.SharedPartition;

    public string Type { get; init; } = RiteType;

    public int Number { get; init; }

    public DateOnly PublishedOnUtc { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    public string GeneratedByModel { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public int BlessedCount { get; init; }

    public int HeresyCount { get; init; }

    // Null when missing: the JSON serializer doesn't know Option, so this is the one place a rite's null lives.
    public RiteSimilarity? Similarity { get; init; }

    public static string IdFor(DateOnly publishedOnUtc) => publishedOnUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static RiteDocument From(Rite rite, int number) => new()
    {
        Id = IdFor(rite.PublishedOnUtc),
        Number = number,
        PublishedOnUtc = rite.PublishedOnUtc,
        Kind = rite.Kind,
        Title = rite.Title,
        Text = rite.Text,
        HereticalTruth = rite.HereticalTruth,
        GeneratedByModel = rite.GeneratedByModel,
        GeneratedAtUtc = Utc.From(rite.GeneratedAtUtc),
        BlessedCount = rite.BlessedCount,
        HeresyCount = rite.HeresyCount,
        Similarity = rite.Similarity.Map(Utc.From).OrNull(),
    };

    public Rite ToRite() => new()
    {
        Number = Number,
        PublishedOnUtc = PublishedOnUtc,
        Kind = Kind,
        Title = Title,
        Text = Text,
        HereticalTruth = HereticalTruth,
        GeneratedByModel = GeneratedByModel,
        GeneratedAtUtc = GeneratedAtUtc,
        BlessedCount = BlessedCount,
        HeresyCount = HeresyCount,
        Similarity = Optional(Similarity),
    };
}

/// <summary>The last rite number given out. Cosmos has no auto-increment, so this document hands them out.</summary>
internal sealed record NumberCounterDocument
{
    public const string CounterId = "rite-number";

    public string Id { get; init; } = CounterId;

    public string Partition { get; init; } = RiteRepository.SharedPartition;

    public string Type { get; init; } = "counter";

    public int LastNumber { get; init; }
}

internal static class Utc
{
    // A UTC time is written with its "Z" and read back as UTC; a local time would be read back as local.
    public static DateTime From(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };

    public static RiteSimilarity From(RiteSimilarity similarity) => similarity with { CreatedAtUtc = From(similarity.CreatedAtUtc) };
}

internal static class OptionExtensions
{
    /// <summary>For the JSON documents only, which store a missing value as null.</summary>
    public static T? OrNull<T>(this Option<T> option) where T : class => option.MatchUnsafe(value => value, () => null);
}
