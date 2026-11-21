namespace DailyMachineSpirit.Functions.Generation.Feasts;

/// <summary>
/// A day the rite is written for (#202): its name, as the card says it, and what the rite is about, in the far future's
/// words, for the writer. Neither names today's holidays or people: the far future forgot them.
/// </summary>
/// <param name="Key">Stable, for tests and logs.</param>
/// <param name="Falls">Whether the feast falls on the day.</param>
public sealed record Feast(string Key, string Name, string Theme, Func<DateOnly, bool> Falls);
