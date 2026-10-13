using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// Judges drafts: their similarity, so "More rites" can find rites like each other; their tags, from the site's
/// vocabulary (<see cref="Tags.RiteTags"/>); and their quality, for the Augury. <see cref="JevJudge"/> is the first;
/// another judge sets its own <see cref="RiteSimilarity.ScoresGeneratorVersion"/> and <see cref="Augury.JudgedBy"/>, so
/// its scores are never compared with Jev's, and gives tags from the same vocabulary.
/// </summary>
public interface IRiteJudge
{
    /// <summary>The draft with its scores and tags, or as it was while judging is turned off.</summary>
    Task<Either<Error, Draft>> Judge(Draft draft, CancellationToken cancellationToken);

    /// <summary>
    /// The tags for scores this judge gave, as <see cref="Judge"/> does; None for scores it can't read (another judge's,
    /// or other questions'). For rites judged before tags existed (<see cref="Tags.TagBackfill"/>).
    /// </summary>
    Option<string[]> TagsFrom(RiteSimilarity similarity);
}
