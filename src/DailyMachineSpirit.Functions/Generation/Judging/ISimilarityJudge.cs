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

    /// <summary>Whether the draft has the scores this judge gives now: true for every draft while it's off.</summary>
    bool HasJudged(Draft draft);

    /// <summary>The tags for scores this judge gave; None for scores it can't read (another judge's).</summary>
    Option<string[]> TagsFrom(RiteSimilarity similarity);
}
