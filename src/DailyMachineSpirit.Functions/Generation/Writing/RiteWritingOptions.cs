namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>The writer of the rites (<c>machine-spirit.json</c>, <c>RiteWriting</c>).</summary>
public sealed record RiteWritingOptions
{
    public const string SectionName = "RiteWriting";

    /// <summary>Deployment names, tried in order: the next one writes only when the one before keeps failing.</summary>
    public string[] Models { get; init; } = [];

    /// <summary>A refill's: nobody waits on it.</summary>
    public Patience Patience { get; init; } = new();

    /// <summary>A rewrite's: the Scribe waits for the page, within the 100 seconds Cloudflare waits.</summary>
    public Patience PatienceWhileTheScribeWaits { get; init; } = new();
}
