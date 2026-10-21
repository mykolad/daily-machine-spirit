using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// One thing a Scribe did to a draft, kept as a lesson for the Augury (#12): the draft as it was, what the Augury
/// thought of it at the time, and the Scribe's note on why, if they left one.
/// </summary>
public sealed record ScribeDecision
{
    public const int MaxNoteLength = 1000;

    public Guid Id { get; init; }

    public Guid DraftId { get; init; }

    public ScribeAction Action { get; init; }

    public RiteKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string HereticalTruth { get; init; } = string.Empty;

    /// <summary>Its place in the Liturgical Calendar before the decision (1 is next); none for a burned draft.</summary>
    public Option<int> CalendarPlace { get; init; }

    /// <summary>
    /// Its place after an anointing, exalting or humbling. None for the other actions, and for decisions saved before it
    /// was kept, when the place after followed from the action.
    /// </summary>
    public Option<int> CalendarPlaceAfter { get; init; }

    /// <summary>The place the Augury alone would give it, without the Scribes' order: how far they disagreed.</summary>
    public Option<int> AuguryPlace { get; init; }

    public Option<float> AuguryQuality { get; init; }

    /// <summary>
    /// Whether the Augury placed it by its quality, as it does the drafts its current judge scored. A draft an older judge
    /// scored is placed by age, as an unjudged one is, so its place says nothing about the Augury's judgment.
    /// </summary>
    public bool AuguryRankedByQuality { get; init; }

    public Option<string> Note { get; init; }

    public DateTime DecidedAtUtc { get; init; }
}

public enum ScribeAction
{
    /// <summary>Anointed it: it joined the Scribes' order, after the rites anointed before it (once, made it next).</summary>
    Anoint,
    /// <summary>Moved it up one place among the anointed.</summary>
    Exalt,
    /// <summary>Moved it down one place among the anointed.</summary>
    Humble,
    /// <summary>Consigned it to the flames.</summary>
    Burn,
    /// <summary>Restored it from the ashes.</summary>
    Restore,
    /// <summary>Amended its words (by hand or by a model's rewrite): the decision keeps them as they were.</summary>
    Amend,
    /// <summary>Released it from the Scribes' order, back to the Augury's.</summary>
    Release,
}
