namespace DailyMachineSpirit.Functions.Api;

/// <summary>A rite as the search field matches it: its words, without code's backticks, and what its suggestion shows.</summary>
/// <param name="Day">"2026-10-14".</param>
/// <param name="Kind">"prayer" or "ritual".</param>
public sealed record RiteIndexEntry(int Number, string Day, string Kind, string Title, string Text, string Truth);
