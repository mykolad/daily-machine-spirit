using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Generation.Scoring;

/// <summary>
/// Scores drafts: their similarity, so "More rites" can find rites like each other, and their quality, for the Augury.
/// <see cref="JevScorer"/> is the first; another scorer sets its own <see cref="RiteSimilarity.ScoresGeneratorVersion"/>
/// and <see cref="Augury.JudgedBy"/>, so its scores are never compared with Jev's.
/// </summary>
public interface IRiteScorer
{
    /// <summary>The draft with its scores, or as it was while scoring is turned off.</summary>
    Task<Either<Error, Draft>> Score(Draft draft, CancellationToken cancellationToken);
}
