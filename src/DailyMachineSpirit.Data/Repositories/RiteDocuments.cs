using System.Globalization;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>An <see cref="Rite"/> as stored: its id is its day, so Cosmos itself allows one rite per day.</summary>
internal sealed class RiteDocument
{
    public const string RiteType = "rite";

    public string Id { get; set; } = string.Empty;
    public string Pk { get; set; } = RiteRepository.Partition;
    public string Type { get; set; } = RiteType;
    public int Number { get; set; }
    public DateOnly PublishedOnUtc { get; set; }
    public RiteKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string HereticalTruth { get; set; } = string.Empty;
    public string GeneratedByModel { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public int BlessedCount { get; set; }
    public int HeresyCount { get; set; }
    public RiteSimilarity? Similarity { get; set; }

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
        Similarity = rite.Similarity is { } similarity ? Utc.From(similarity) : null,
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
        Similarity = Similarity,
    };
}

/// <summary>The last rite number given out. Cosmos has no auto-increment, so this document hands them out.</summary>
internal sealed class NumberCounterDocument
{
    public const string CounterId = "rite-number";

    public string Id { get; set; } = CounterId;
    public string Pk { get; set; } = RiteRepository.Partition;
    public string Type { get; set; } = "counter";
    public int LastNumber { get; set; }
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

    public static RiteSimilarity From(RiteSimilarity similarity) => new()
    {
        ScoresGeneratorVersion = similarity.ScoresGeneratorVersion,
        Scores = similarity.Scores,
        CreatedAtUtc = From(similarity.CreatedAtUtc),
    };
}
