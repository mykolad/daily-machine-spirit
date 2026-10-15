using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <param name="Found">Every rite holding the words, on every page, newest first: counted and indexed by month.</param>
/// <param name="Shown">This page's rites, newest first.</param>
/// <param name="OlderThan">Where the next page starts, or None when these are the oldest.</param>
public sealed record SearchResults(IReadOnlyList<RiteDay> Found, IReadOnlyList<Rite> Shown, Option<int> OlderThan);
