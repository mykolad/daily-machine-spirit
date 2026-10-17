using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// Who an entry in the record (#9) names: the Machine Spirit, or a Scribe by the end of their id. The one place that
/// turns an actor into words, where the names the Scribes will choose for themselves go.
/// </summary>
public static class ScribeNames
{
    public static string Of(string actor) => actor switch
    {
        AuditEntry.MachineSpirit => "The Machine Spirit",
        AuditEntry.StagingScribe => "A Scribe on staging",
        // Enough of the id to tell two Scribes apart, too little to be worth keeping anywhere else.
        _ => $"A Scribe (…{actor[Math.Max(0, actor.Length - 6)..]})",
    };
}
