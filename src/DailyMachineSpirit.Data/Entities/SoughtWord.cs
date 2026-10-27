namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A word a reader searches for, and the subjects it names (their tags, "builds" for "build"). A rite holds it when its
/// title, text or Heretical Truth does, or when it's filed under one of those subjects.
/// </summary>
public sealed record SoughtWord(string Word, IReadOnlyList<string> Subjects)
{
    /// <summary>A word that names no subject.</summary>
    public static implicit operator SoughtWord(string word) => new(word, []);
}
