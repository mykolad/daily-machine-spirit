namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The amending page's and the Precepts' script (#85): their slow actions, a rewrite and a distillation, take seconds,
/// so a pressed button says what's happening and can't be pressed again. A rewrite is posted in the background and its
/// form takes the old one's place, so the address stays the amending page's and a reload never resends it. Asking for
/// a rewrite needs a note, so its button waits for one. Without the script (or when anything goes wrong) the forms
/// work as they are.
/// </summary>
public static class SlowActionScript
{
    public static readonly string Js = EmbeddedScripts.Read("Scriptorium.SlowActionScript.js");
}
