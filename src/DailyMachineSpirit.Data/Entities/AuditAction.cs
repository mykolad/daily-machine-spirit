namespace DailyMachineSpirit.Data.Entities;

/// <summary>What an <see cref="AuditEntry"/> records: the Scribes' actions, and the Machine Spirit's own.</summary>
public enum AuditAction
{
    /// <summary>A Scribe made a draft the next to be published.</summary>
    Anoint,
    /// <summary>A Scribe moved a draft up one place.</summary>
    Exalt,
    /// <summary>A Scribe moved a draft down one place.</summary>
    Humble,
    /// <summary>A Scribe consigned a draft to the flames.</summary>
    Burn,
    /// <summary>A Scribe restored a draft from the ashes.</summary>
    Restore,
    /// <summary>A Scribe amended a draft's words.</summary>
    Amend,
    /// <summary>A Scribe summoned new drafts.</summary>
    Summon,
    /// <summary>A Scribe let the Augury order every draft again.</summary>
    LetTheAuguryDecide,
    /// <summary>The Machine Spirit wrote a draft (a refill).</summary>
    Write,
    /// <summary>The Machine Spirit published the day's rite.</summary>
    Publish,
    /// <summary>The Precepts were distilled again (#12): weekly by the Machine Spirit, or when a Scribe asked.</summary>
    Distil,
    /// <summary>A Scribe added a precept of their own.</summary>
    AddPrecept,
    /// <summary>A Scribe changed a precept's words.</summary>
    EditPrecept,
    /// <summary>A Scribe pinned a precept, so distilling again keeps it.</summary>
    PinPrecept,
    /// <summary>A Scribe unpinned a precept, so the next distillation may replace it.</summary>
    UnpinPrecept,
    /// <summary>A Scribe deleted a precept.</summary>
    DeletePrecept,
    /// <summary>A waiting draft judging couldn't score was judged again: by a refill (the Machine Spirit) or a Scribe.</summary>
    JudgeAgain,
}
