using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// Who an entry in the Record (#9) names: the Machine Spirit, a Scribe by the name they chose (#84), or by the end of
/// their id until they choose one. The Record keeps ids, never names, so a new name shows on every past entry too.
/// </summary>
public static class ScribeNames
{
    public const string MachineSpirit = "The Machine Spirit";

    public const string OnStaging = "A Scribe on staging";

    public static string Of(string actor, IReadOnlyList<ScribeName> names) => actor switch
    {
        AuditEntry.MachineSpirit => MachineSpirit,
        AuditEntry.StagingScribe => OnStaging,
        _ => names.Where(name => name.AccessId == actor).Select(name => name.Name).HeadOrNone().IfNone(() => Unnamed(actor)),
    };

    // Enough of the id to tell two Scribes apart, too little to be worth keeping anywhere else.
    public static string Unnamed(string actor) => $"A Scribe (…{actor[Math.Max(0, actor.Length - 6)..]})";
}
