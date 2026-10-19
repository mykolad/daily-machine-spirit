using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>
/// A published rite as the Expurgator sees it, and the version of its words: the forms carry it, so words chosen on a
/// page shown before another erasure are refused rather than landing on other words.
/// </summary>
public sealed record ExpurgatableRite(Rite Rite, string Version);
