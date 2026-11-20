namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>The Scriptorium's pages, as its tab bar lists them.</summary>
public enum ScriptoriumTab
{
    Calendar,
    Ashes,
    Precepts,
    Record,

    // Shown only while false truths wait for a Scribe (#199): the backfill is done once.
    FalseTruths,
}
