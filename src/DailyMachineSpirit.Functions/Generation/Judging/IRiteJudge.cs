using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// Judges drafts: their similarity, so "More rites" can find rites like each other; their tags, from the site's
/// vocabulary (<see cref="Tags.RiteTags"/>); and their quality, for the Augury. <see cref="RiteJudge"/> asks Jev for the
/// first two and a model, against a rubric, for the quality. Another judge sets its own
/// <see cref="RiteSimilarity.ScoresGeneratorVersion"/> and <see cref="Augury.JudgedBy"/>, so its scores and qualities
/// are never compared with these, and gives tags from the same vocabulary.
/// </summary>
public interface IRiteJudge
{
    /// <summary>
    /// Whether the draft has everything this judge gives now: else it's judged again (<see cref="DraftJudging"/>). A
    /// quality another judge gave doesn't count, since the Augury only compares qualities from the same judge.
    /// </summary>
    bool HasJudged(Draft draft);

    /// <summary>
    /// The draft with its scores, tags and quality; an error when any part couldn't be judged, so the draft waits to be
    /// judged again whole.
    /// </summary>
    Task<Either<Error, Draft>> Judge(Draft draft, CancellationToken cancellationToken);

    /// <summary>
    /// The tags for scores this judge gave, as <see cref="Judge"/> does; None for scores it can't read (another judge's,
    /// or other questions'). For rites judged before tags existed (<see cref="Tags.TagBackfill"/>).
    /// </summary>
    Option<string[]> TagsFrom(RiteSimilarity similarity);
}
