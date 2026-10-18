namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

public class ScriptoriumOptions
{
    public const string SectionName = "Scriptorium";

    /// <summary>
    /// Lets everyone who reaches the Scriptorium in, with no sign-in: only for an app that admits its owner's address
    /// alone (staging), and a local run. Elsewhere the Scribes sign in with Cloudflare Access, and with neither, nobody
    /// gets in.
    /// </summary>
    public bool WithoutSignIn { get; set; }
}
