namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The script of the calendar and the Ashes, on top of their forms and links. It folds every rite but the next to
/// publish into its row, asks why before a burn, and offers to undo a burn on the calendar or a restore in the Ashes. An
/// action is posted in the background and the new page takes the old one's place, every rite that moved sliding from
/// where it was, so a Scribe sees what changed; a subject's chip loads the page narrowed to it the same way. Without it
/// (or when anything goes wrong) the forms and links work as they are: post, redirect, a new page, every rite open.
/// </summary>
public static class ScriptoriumScript
{
    public static readonly string Js = EmbeddedScripts.Read("Scriptorium.ScriptoriumScript.js");
}
