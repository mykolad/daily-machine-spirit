using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <param name="Found">How many rites hold the words, on every page together.</param>
/// <param name="Shown">This page's rites, newest first.</param>
/// <param name="OlderThan">Where the next page starts, or None when these are the oldest.</param>
public sealed record SearchResults(int Found, IReadOnlyList<Rite> Shown, Option<int> OlderThan);
