namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// A rite already written, as a new one must not repeat it: its title, and its Heretical Truth, which says the habit and
/// the lesson (a new title on the same lesson is still the same rite).
/// </summary>
public sealed record TakenRite(string Title, string HereticalTruth);
