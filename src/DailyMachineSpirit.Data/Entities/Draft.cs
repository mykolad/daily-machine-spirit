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

    public RiteWriting Writing { get; init; } = new();

    /// <summary>Copied to the rite when it's published, for "More rites".</summary>
    public Option<RiteSimilarity> Similarity { get; init; }

    public Option<Augury> Augury { get; init; }

    public Option<DateOnly> PublishedOnUtc { get; init; }

    public Option<DateTime> BurnedAtUtc { get; init; }

    public Rite ToRite(DateOnly publishedOnUtc) => new()
    {
        PublishedOnUtc = publishedOnUtc,
        Writing = Writing,
        Similarity = Similarity,
    };
}
