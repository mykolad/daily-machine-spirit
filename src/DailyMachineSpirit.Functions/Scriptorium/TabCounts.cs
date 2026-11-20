namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// What the tab bar counts: the waiting rites the Scribes placed and those the Augury places, the burned ones, the
/// precepts, the Record's entries, and the false truths proposed for published rites (#199).
/// </summary>
public sealed record TabCounts(int PlacedByScribes, int PlacedByAugury, int Ashes, int Precepts, int RecordEntries, int ProposedFalseTruths);
