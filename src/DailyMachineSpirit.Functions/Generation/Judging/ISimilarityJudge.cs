using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// A draft's similarity scores, for "More rites", and its tags from the site's vocabulary (<see cref="Tags.RiteTags"/>):
/// <see cref="JevJudge"/> for now. Another judge sets its own <see cref="RiteSimilarity.ScoresGeneratorVersion"/>, so
/// its scores are never compared with these.
/// </summary>
public interface ISimilarityJudge
{
    /// <summary>The draft with its scores and tags; the draft as it was while the judge is off.</summary>
    Task<Either<Error, Draft>> Judge(Draft draft, CancellationToken cancellationToken);

    bool NeedsJudging(Draft draft);
}
