namespace DailyMachineSpirit.Data.Entities;

public enum DraftState
{
    Waiting,
    Published,
    /// <summary>Consigned to the flames by a Scribe: hidden, never deleted, and can be restored.</summary>
    Burned,
}
