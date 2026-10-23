namespace DailyMachineSpirit.Data.Entities;

/// <summary>Whose words a precept is.</summary>
public enum PreceptOrigin
{
    /// <summary>As a model distilled it from the Scribes' judgments.</summary>
    Distilled,

    /// <summary>Added by a Scribe, in their own words.</summary>
    Written,

    /// <summary>Distilled or written, then edited by a Scribe.</summary>
    Amended,

    /// <summary>A Scribe's words, kept before it was known whether they wrote or amended it.</summary>
    ScribesWords,
}
