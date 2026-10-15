namespace DailyMachineSpirit.Data.Entities;

/// <summary>A rite's number and day without its words: enough to count rites and index them by month.</summary>
public sealed record RiteDay(int Number, DateOnly PublishedOnUtc);
