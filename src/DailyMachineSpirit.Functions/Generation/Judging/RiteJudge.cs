using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// The similarity judge's scores and tags, and the quality judge's quality, asked at once. While the similarity judge is
/// off, the draft gets its quality alone; when either fails, it's an error, so no draft is judged in part.
/// </summary>
public sealed class RiteJudge : IRiteJudge
{
    private readonly ISimilarityJudge similarity;
    private readonly IQualityJudge quality;

    public RiteJudge(ISimilarityJudge similarity, IQualityJudge quality)
    {
        this.similarity = similarity;
        this.quality = quality;
    }

    public bool NeedsJudging(Draft draft) => similarity.NeedsJudging(draft) || quality.NeedsJudging(draft);

    public async Task<Either<Error, Draft>> Judge(Draft draft, CancellationToken cancellationToken)
    {
        var scored = similarity.Judge(draft, cancellationToken);
        var judgedQuality = quality.Judge(draft, cancellationToken);
        var (withScores, augury) = (await scored, await judgedQuality);
        return from judged in withScores
               from judgment in augury
               select judged with { Augury = Some(judgment) };
    }

    public Option<string[]> TagsFrom(RiteSimilarity scores) => similarity.TagsFrom(scores);
}
