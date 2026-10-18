namespace DailyMachineSpirit.Functions.Generation;

/// <summary>
/// How rites are written: fixed in code, not read from configuration, as no environment sets them. Tests change them, to
/// tell their fake models apart, keep a backlog small and not wait between tries.
/// </summary>
public class GenerationOptions
{
    /// <summary>Deployment names, tried in order: the next one writes only when the one before keeps failing.</summary>
    public string[] Models { get; init; } = ["gpt-6-sol", "gpt-6-luna"];

    public string DuplicateCheckModel { get; init; } = "gpt-6-luna";

    /// <summary>How many drafts wait in the backlog: a few weeks' worth, so a moderator visiting weekly always has a choice.</summary>
    public int BacklogSize { get; init; } = 20;

    /// <summary>The pause between a refill's tries of one model.</summary>
    public int RetryDelaySeconds { get; init; } = 30;
}
