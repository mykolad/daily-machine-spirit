namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Precepts' script, on top of their forms and links: a precept's words open to edit in place (its pencil, or a
/// click on the words), and Add the Precept waits for words. Without it, the pencil is a link to the page with those
/// words open (<c>?edit=</c>).
/// </summary>
public static class PreceptsScript
{
    public static readonly string Js = EmbeddedScripts.Read("Scriptorium.PreceptsScript.js");
}
