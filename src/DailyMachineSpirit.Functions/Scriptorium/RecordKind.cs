namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>What the Record's filter narrows it to: each <see cref="Data.Entities.AuditAction"/> is one of these.</summary>
public enum RecordKind
{
    Published,
    Written,
    Judged,
    Moved,
    Burned,
    Other,
}
