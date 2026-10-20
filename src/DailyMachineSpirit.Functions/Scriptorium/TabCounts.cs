namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// What the tab bar counts: the waiting rites the Scribes placed and those the Augury places, the burned ones, the
/// precepts and the Record's entries.
/// </summary>
public sealed record TabCounts(int PlacedByScribes, int PlacedByAugury, int Ashes, int Precepts, int RecordEntries);
