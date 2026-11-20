namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The pages' only script: breaking the seal, copying links and text, the toast, the countdown's tick, voting,
/// drawing a rite, and stepping to the rite before or after by key or swipe.
/// Everything it touches is already in the server's HTML, and without it a rite's truth is still shown (a noscript style).
/// </summary>
public static class Script
{
    public static readonly string Js = EmbeddedScripts.Read("Pages.Script.js");
}
