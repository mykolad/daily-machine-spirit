using LanguageExt;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// The rites just before and after one, by number: None before the first, and after the newest.
/// <paramref name="NextIsNewest"/> says the one after is the newest, which Today shows.
/// </summary>
public sealed record RiteNeighbours(Option<RiteHeading> Previous, Option<RiteHeading> Next, bool NextIsNewest);
