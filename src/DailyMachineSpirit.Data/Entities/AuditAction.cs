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
}
