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

    /// <summary>Copied to the rite when it's published.</summary>
    public Option<RiteSimilarity> Similarity { get; init; }

    public Option<string[]> Tags { get; init; }

    public Option<Augury> Augury { get; init; }

    public Option<DateOnly> PublishedOnUtc { get; init; }

    public Option<DateTime> BurnedAtUtc { get; init; }

    /// <summary>
    /// Who burned it (a Scribe's Access id), while it's burned. None for a burn the Record has no entry for: older than
    /// the Record.
    /// </summary>
    public Option<ScribeId> BurnedBy { get; init; }

    /// <summary>Its place in the calendar when it was burned (1 is next), while it's burned.</summary>
    public Option<int> BurnedFromPlace { get; init; }

    /// <summary>Why it was burned, while it's burned: the Scribe's note, if they wrote one.</summary>
    public Option<string> BurnReason { get; init; }

    /// <summary>The title of the rite it repeats, when it still did after being written again: for the Scribes to judge.</summary>
    public Option<string> Resembles { get; init; }

    public Rite ToRite(DateOnly publishedOnUtc) => new()
    {
        PublishedOnUtc = publishedOnUtc,
        Writing = Writing,
        Similarity = Similarity,
        Tags = Tags,
    };
}
