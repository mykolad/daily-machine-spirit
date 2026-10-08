using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Generation.Scoring;

/// <summary>
/// Scores rites, so "More rites" can find the ones like each other. <see cref="JevScorer"/> is the first; another scorer
/// sets its own <see cref="RiteSimilarity.ScoresGeneratorVersion"/>, so its scores are never compared with Jev's.
/// </summary>
public interface IRiteScorer
{
    /// <summary>The rite's scores, or None while scoring is turned off.</summary>
    Task<Either<Error, Option<RiteSimilarity>>> Score(Rite rite, CancellationToken cancellationToken);
}
