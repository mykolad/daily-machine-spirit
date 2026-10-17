using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <param name="Found">Every rite the search found, on every page, newest first: counted, indexed by month, and their
/// tags offered to narrow the search by.</param>
/// <param name="Shown">This page's rites, newest first.</param>
/// <param name="OlderThan">Where the next page starts, or None when these are the oldest.</param>
public sealed record SearchResults(IReadOnlyList<FoundRite> Found, IReadOnlyList<Rite> Shown, Option<int> OlderThan);
