namespace DailyMachineSpirit.Functions.Generation;

/// <summary>The drafts waiting for the Scribes (<c>machine-spirit.jsonc</c>, <c>Backlog</c>).</summary>
public sealed record BacklogOptions
{
    public const string SectionName = "Backlog";

    public int DraftsWaiting { get; init; }

    /// <summary>The newest rites, and burned drafts, the writer's prompt lists; the duplicate check reads every rite.</summary>
    public int RecentRitesInPrompt { get; init; }

    public int RewritesOfARepeatedRite { get; init; }
}
