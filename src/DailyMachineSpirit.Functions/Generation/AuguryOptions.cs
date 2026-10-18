namespace DailyMachineSpirit.Functions.Generation;

/// <summary>How the Augury orders the waiting drafts (<c>machine-spirit.json</c>, <c>Augury</c>).</summary>
public sealed record AuguryOptions
{
    public const string SectionName = "Augury";

    /// <summary>How many of the newest rites a draft is compared with.</summary>
    public int RecentRitesCompared { get; init; }

    /// <summary>Taken off a draft's quality for each bit of resemblance (0 to 1) to those rites.</summary>
    public float ResemblancePenalty { get; init; }

    /// <summary>Taken off when it's of the same kind as the rite before it.</summary>
    public float SameKindPenalty { get; init; }
}
