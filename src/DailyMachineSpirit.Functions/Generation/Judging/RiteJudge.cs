using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// Jev's similarity scores and tags, and the <see cref="RubricJudge"/>'s quality, asked at once. While Jev is off, the
/// draft gets its quality alone; when either fails, the draft waits to be judged again whole.
/// </summary>
public sealed class RiteJudge : IRiteJudge
{
    private readonly JevJudge jev;
    private readonly RubricJudge rubric;

    public RiteJudge(JevJudge jev, RubricJudge rubric)
    {
        this.jev = jev;
        this.rubric = rubric;
    }

    // Judged while Jev was off, a draft has its quality but no scores: once Jev is on, it's judged again for them.
    public bool HasJudged(Draft draft)
        => draft.Augury.Exists(augury => augury.JudgedBy == RubricJudge.Version) && (!jev.Enabled || draft.Similarity.IsSome);

    public async Task<Either<Error, Draft>> Judge(Draft draft, CancellationToken cancellationToken)
    {
        var scored = jev.Judge(draft, cancellationToken);
        var quality = rubric.Judge(draft, cancellationToken);
        var (withScores, augury) = (await scored, await quality);
        return from judged in withScores
               from judgment in augury
               select judged with { Augury = Some(judgment) };
    }

    public Option<string[]> TagsFrom(RiteSimilarity similarity) => jev.TagsFrom(similarity);
}
