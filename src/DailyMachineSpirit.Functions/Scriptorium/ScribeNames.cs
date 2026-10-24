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

    public static string Of(Actor actor, IReadOnlyList<ScribeName> names)
        => actor.Scribe.Match(
            Some: scribe => names.Where(name => name.Scribe == scribe).Select(name => name.Name).HeadOrNone().IfNone(() => Unnamed(scribe)),
            None: () => MachineSpirit);

    // Enough of the id to tell two Scribes apart, too little to be worth keeping anywhere else. Staging's one shared
    // Scribe has no id to show.
    public static string Unnamed(ScribeId scribe)
        => scribe == ScribeId.Staging ? OnStaging : $"A Scribe (…{scribe.Value[Math.Max(0, scribe.Value.Length - 6)..]})";
}
