using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// Judges drafts: their similarity, so "More rites" can find rites like each other, and their quality, for the Augury.
/// <see cref="JevJudge"/> is the first; another judge sets its own <see cref="RiteSimilarity.ScoresGeneratorVersion"/>
/// and <see cref="Augury.JudgedBy"/>, so its scores are never compared with Jev's.
/// </summary>
public interface IRiteJudge
{
    /// <summary>The draft with its scores, or as it was while judging is turned off.</summary>
    Task<Either<Error, Draft>> Judge(Draft draft, CancellationToken cancellationToken);
}
