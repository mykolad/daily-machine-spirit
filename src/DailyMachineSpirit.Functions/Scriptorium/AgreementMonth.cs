using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// How far the Scribes agreed with the Augury in one month (#12): how many of its top picks they burned, and how far
/// they moved the drafts they moved from where it had placed them.
/// </summary>
/// <param name="Month">The month's first day (UTC).</param>
/// <param name="Judgments">Every decision the Scribes made that month.</param>
/// <param name="Burns">Drafts consigned to the flames.</param>
/// <param name="TopPicksBurned">Burned drafts the Augury had judged and put among its top picks.</param>
/// <param name="Moves">Anointed, exalted and humbled drafts the Augury had judged.</param>
/// <param name="AverageDistance">How many places, on average, the moves took a draft from the Augury's place for it.</param>
public sealed record AgreementMonth(DateOnly Month, int Judgments, int Burns, int TopPicksBurned, int Moves, Option<double> AverageDistance);
