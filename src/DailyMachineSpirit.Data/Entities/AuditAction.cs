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
    Release,

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

    // A deleted precept put back by the Undo that follows the delete.
    RestorePrecept,

    // A waiting draft judging couldn't score, judged again: by a refill (the Machine Spirit) or a Scribe.
    JudgeAgain,

    // A Scribe chose the name the Record shows them by (#84).
    ChooseName,

    // An Expurgator erased words from a published rite, everywhere they were kept. The entry names the parts, never the
    // words.
    Expurgate,
}
