using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// A draft's quality for the Augury: <see cref="RubricJudge"/> for now. The Augury compares only qualities from the same
/// judge (<see cref="Augury.JudgedBy"/>).
/// </summary>
public interface IQualityJudge
{
    Task<Either<Error, Augury>> Judge(Draft draft, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this judge gave the quality, as it judges now: one from another judge, or an older version of this one,
    /// can't be compared with its own.
    /// </summary>
    bool HasJudged(Augury augury);
}
