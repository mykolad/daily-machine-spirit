using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>Who an entry in the record (#9) names: a Scribe by the name set for their id, or the Machine Spirit.</summary>
public static class ScribeNames
{
    public static string Of(string actor, IReadOnlyDictionary<string, string> names) => actor switch
    {
        AuditEntry.MachineSpirit => "The Machine Spirit",
        AuditEntry.StagingScribe => "A Scribe on staging",
        _ when names.TryGetValue(actor, out var name) => name,
        // Enough of the id to tell two unnamed Scribes apart, too little to be worth keeping anywhere else.
        _ => $"A Scribe (…{actor[Math.Max(0, actor.Length - 6)..]})",
    };
}
