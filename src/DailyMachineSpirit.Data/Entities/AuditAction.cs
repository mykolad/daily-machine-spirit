namespace DailyMachineSpirit.Data.Entities;

/// <summary>What an <see cref="AuditEntry"/> records: the Scribes' actions, and the Machine Spirit's own.</summary>
public enum AuditAction
{
    Anoint,
    Exalt,
    Humble,
    Burn,
    Restore,
    Amend,
    Summon,
    LetTheAuguryDecide,

    // The Machine Spirit's own.
    Write,
    Publish,

    // Weekly by the Machine Spirit, or when a Scribe asks (#12).
    Distil,

    // A Scribe's changes to the Precepts. A pinned precept stays when they're distilled again.
    AddPrecept,
    EditPrecept,
    PinPrecept,
    UnpinPrecept,
    DeletePrecept,
    /// <summary>A waiting draft judging couldn't score was judged again: by a refill (the Machine Spirit) or a Scribe.</summary>
    JudgeAgain,
}
