using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>The rites just before and after one, by number: None before the first, and after the newest.</summary>
public sealed record RiteNeighbours(Option<RiteHeading> Previous, Option<RiteHeading> Next);
