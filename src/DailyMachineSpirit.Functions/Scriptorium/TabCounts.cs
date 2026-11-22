namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// What the tab bar counts: the waiting rites kept for their feasts (#202), those the Scribes placed and those the Augury
/// places, the burned ones, the precepts, the Record's entries, and the false truths proposed for published rites (#199).
/// </summary>
public sealed record TabCounts(int ForFeasts, int PlacedByScribes, int PlacedByAugury, int Ashes, int Precepts, int RecordEntries, int ProposedFalseTruths);
