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

    bool NeedsJudging(Draft draft);
}
