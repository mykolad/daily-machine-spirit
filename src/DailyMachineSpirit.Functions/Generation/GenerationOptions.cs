namespace DailyMachineSpirit.Functions.Generation;

public class GenerationOptions
{
    public const string SectionName = "Generation";

    /// <summary>Deployment names, tried in order: the next one writes only when the one before keeps failing.</summary>
    public string[] Models { get; set; } = ["gpt-6-sol", "gpt-6-luna"];

    /// <summary>How many drafts wait in the backlog: a few weeks' worth, so a moderator visiting weekly always has a choice.</summary>
    public int BacklogSize { get; set; } = 20;

    public int AttemptsPerModel { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 30;

    /// <summary>How many recent titles the prompt lists, so the model picks another subject.</summary>
    public int RecentRitesInPrompt { get; set; } = 30;
}
