namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A reader's office in the shrine (#201), held from <paramref name="Threshold"/> rites judged on.</summary>
/// <param name="Article">"a" or "an", as the Breviary's kicker and the toast say it.</param>
/// <param name="Insignia">Its glyph (<see cref="Svg"/>), in currentColor.</param>
public sealed record Rank(string Name, string Article, int Threshold, string Insignia);
