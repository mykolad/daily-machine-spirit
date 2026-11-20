namespace DailyMachineSpirit.Functions;

/// <summary>
/// The pages' scripts, each a JavaScript file of its own beside the class that names it, so an editor and the build's
/// syntax check (<c>node --check</c>) read them as JavaScript; embedded in the assembly and read once, at startup.
/// </summary>
public static class EmbeddedScripts
{
    private const string Prefix = "DailyMachineSpirit.Functions.";

    /// <param name="name">The file's path under the project, with dots for slashes: <c>Pages.Script.js</c>.</param>
    public static string Read(string name)
    {
        using var stream = typeof(EmbeddedScripts).Assembly.GetManifestResourceStream(Prefix + name)
            ?? throw new InvalidOperationException($"The embedded script {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
