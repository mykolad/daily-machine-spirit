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
}
