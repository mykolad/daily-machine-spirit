using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A rite written ahead, waiting in the backlog. Each day the top of the Liturgical Calendar is published as that day's
/// <see cref="Rite"/>; the draft stays, marked published, so nothing a moderator or the Augury acted on disappears.
/// </summary>
public sealed record Draft
{
    public Guid Id { get; init; }

    public DraftState State { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    public string GeneratedByModel { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>Copied to the rite when it's published, for "More rites".</summary>
    public Option<RiteSimilarity> Similarity { get; init; }

    public Option<Augury> Augury { get; init; }

    public Option<DateOnly> PublishedOnUtc { get; init; }

    public Option<DateTime> BurnedAtUtc { get; init; }

    public Rite ToRite(DateOnly publishedOnUtc) => new()
    {
        PublishedOnUtc = publishedOnUtc,
        Kind = Kind,
        Title = Title,
        Text = Text,
        HereticalTruth = HereticalTruth,
        GeneratedByModel = GeneratedByModel,
        GeneratedAtUtc = GeneratedAtUtc,
        Similarity = Similarity,
    };
}

public enum DraftState
{
    Waiting,
    Published,
    /// <summary>Consigned to the flames by a Scribe: hidden, never deleted, and can be restored.</summary>
    Burned,
}

/// <summary>How good a judge thinks a draft is: the Augury publishes the better ones first.</summary>
public sealed record Augury
{
    /// <summary>From 0 (weak) to 1 (excellent).</summary>
    public float Quality { get; init; }

    /// <summary>Which judge, and which version of its question: qualities from different judges aren't comparable.</summary>
    public string JudgedBy { get; init; } = string.Empty;

    public DateTime JudgedAtUtc { get; init; }
}
