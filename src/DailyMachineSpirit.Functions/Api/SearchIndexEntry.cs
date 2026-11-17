namespace DailyMachineSpirit.Functions.Api;

/// <summary>A rite as the search field matches it: its words as stored, its subjects, and what its suggestion shows.</summary>
/// <param name="Day">"2026-10-14".</param>
/// <param name="Kind">"prayer" or "ritual".</param>
/// <param name="Ailment">The trouble it's for, which a word typed may be in (#197); empty for a rite without one.</param>
/// <param name="Tags">Its subjects' keys, which a word typed may name.</param>
public sealed record SearchIndexEntry(int Number, string Day, string Kind, string Title, string Text, string Truth, string Ailment, IReadOnlyList<string> Tags);
