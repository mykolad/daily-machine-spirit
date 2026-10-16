using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>Who an entry in the record (#9) names: a Scribe by the name set for their id, or the Machine Spirit.</summary>
public static class ScribeNames
{
    public static string Of(string actor, IReadOnlyList<ScribeName> names) => actor switch
    {
        AuditEntry.MachineSpirit => "The Machine Spirit",
        AuditEntry.StagingScribe => "A Scribe on staging",
        // Enough of the id to tell two unnamed Scribes apart, too little to be worth keeping anywhere else.
        _ => names.Where(scribe => scribe.Id == actor).Select(scribe => scribe.Name).HeadOrNone()
            .IfNone(() => $"A Scribe (…{actor[Math.Max(0, actor.Length - 6)..]})"),
    };
}
