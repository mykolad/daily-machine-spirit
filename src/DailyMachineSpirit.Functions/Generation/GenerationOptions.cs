namespace DailyMachineSpirit.Functions.Generation;

public class GenerationOptions
{
    public const string SectionName = "Generation";

    /// <summary>Deployment names, tried in order: the next one writes only when the one before keeps failing.</summary>
    public string[] Models { get; set; } = ["gpt-6-sol", "gpt-6-luna"];

    /// <summary>
    /// Whether the midnight timer publishes a rite (and then refills the backlog). Off for production until launch, so
    /// no rite is published before anyone can read it or a Scribe can choose it; the Scriptorium still summons drafts.
    /// </summary>
    public bool PublishDaily { get; set; } = true;

    /// <summary>How many drafts wait in the backlog: a few weeks' worth, so a moderator visiting weekly always has a choice.</summary>
    public int BacklogSize { get; set; } = 20;

    public int AttemptsPerModel { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 30;

    public int RecentRitesInPrompt { get; set; } = 30;

    public string DuplicateCheckModel { get; set; } = "gpt-6-luna";

    /// <summary>Tried in order within <see cref="Precepts.PreceptCheck.TimeAllowed"/>, so another answers when one fails.</summary>
    public string[] PreceptCheckModels { get; set; } = ["gpt-6-luna", "gpt-6-sol"];

    public string ScribeNameModel { get; set; } = "gpt-6-luna";

    /// <summary>How many times a draft that repeats a rite is written again before it's kept, marked for the Scribes.</summary>
    public int DuplicateRewrites { get; set; } = 2;
}
