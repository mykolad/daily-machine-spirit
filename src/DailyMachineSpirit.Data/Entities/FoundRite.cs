namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A rite a search found, without its words: its number and day, to count the rites and index them by month, and its
/// tags, to narrow the search by.
/// </summary>
public sealed record FoundRite(int Number, DateOnly PublishedOnUtc, IReadOnlyList<string> Tags)
{
    public RiteDay Day => new(Number, PublishedOnUtc);
}
