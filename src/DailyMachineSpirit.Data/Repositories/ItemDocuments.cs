using System.Globalization;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>An <see cref="Item"/> as stored: its id is its day, so Cosmos itself allows one item per day.</summary>
internal sealed class ItemDocument
{
    public const string ItemType = "item";

    public string Id { get; set; } = string.Empty;
    public string Pk { get; set; } = ItemRepository.Partition;
    public string Type { get; set; } = ItemType;
    public int Number { get; set; }
    public DateOnly PublishedOnUtc { get; set; }
    public ItemKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string HereticalTruth { get; set; } = string.Empty;
    public string GeneratedByModel { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public int BlessedCount { get; set; }
    public int HeresyCount { get; set; }
    public ItemSimilarity? Similarity { get; set; }

    public static string IdFor(DateOnly publishedOnUtc) => publishedOnUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static ItemDocument From(Item item, int number) => new()
    {
        Id = IdFor(item.PublishedOnUtc),
        Number = number,
        PublishedOnUtc = item.PublishedOnUtc,
        Kind = item.Kind,
        Title = item.Title,
        Text = item.Text,
        HereticalTruth = item.HereticalTruth,
        GeneratedByModel = item.GeneratedByModel,
        GeneratedAtUtc = Utc.From(item.GeneratedAtUtc),
        BlessedCount = item.BlessedCount,
        HeresyCount = item.HeresyCount,
        Similarity = item.Similarity,
    };

    public Item ToItem() => new()
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

/// <summary>The last item number given out. Cosmos has no auto-increment, so this document hands them out.</summary>
internal sealed class NumberCounterDocument
{
    public const string CounterId = "item-number";

    public string Id { get; set; } = CounterId;
    public string Pk { get; set; } = ItemRepository.Partition;
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
}
