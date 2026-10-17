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
}
