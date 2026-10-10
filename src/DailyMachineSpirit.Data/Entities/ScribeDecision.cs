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

    /// <summary>The place the Augury alone would give it, without the Scribes' order: how far they disagreed.</summary>
    public Option<int> AuguryPlace { get; init; }

    public Option<float> AuguryQuality { get; init; }

    public Option<string> Note { get; init; }

    public DateTime DecidedAtUtc { get; init; }
}

public enum ScribeAction
{
    /// <summary>Made it the next to be published.</summary>
    Anoint,
    /// <summary>Moved it up one place.</summary>
    Exalt,
    /// <summary>Moved it down one place.</summary>
    Humble,
    /// <summary>Consigned it to the flames.</summary>
    Burn,
    /// <summary>Restored it from the ashes.</summary>
    Restore,
}
