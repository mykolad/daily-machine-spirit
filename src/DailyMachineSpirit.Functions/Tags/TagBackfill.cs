using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Judging;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Tags;

/// <summary>
/// Gives tags to the rites and drafts judged before tags existed, from their stored scores (the judge reads its own:
/// <see cref="IRiteJudge.TagsFrom"/>), each time the app starts. After the first run there's nothing left, and it's one
/// small query for rites and one for drafts. Scores the judge can't read stay untagged. A failure is logged, and the
/// next start tries again.
/// </summary>
public sealed class TagBackfill : BackgroundService
{
    private readonly IRiteRepository rites;
    private readonly IDraftRepository drafts;
    private readonly IRiteJudge judge;
    private readonly ILogger<TagBackfill> logger;

    public TagBackfill(IRiteRepository rites, IDraftRepository drafts, IRiteJudge judge, ILogger<TagBackfill> logger)
    {
        this.rites = rites;
        this.drafts = drafts;
        this.judge = judge;
        this.logger = logger;
    }

    /// <summary>How many rites and drafts it tagged.</summary>
    public async Task<Either<Error, int>> Run(CancellationToken cancellationToken)
    {
        // Drafts first: a draft published meanwhile then takes its tags to its rite, as publishing reads the stored
        // draft, and a rite published before its draft was tagged is still untagged when the rites are looked at.
        var taggedDrafts = await drafts.GetUntagged(cancellationToken)
            .BindAsync(untagged => TagEach(untagged, draft => draft.Similarity, (draft, tags) => drafts.SaveTags(draft.Id, tags, cancellationToken)));
        var taggedRites = await rites.GetUntagged(cancellationToken)
            .BindAsync(untagged => TagEach(untagged, rite => rite.Similarity, (rite, tags) => rites.SaveTags(rite.PublishedOnUtc, tags, cancellationToken)));
        return taggedDrafts.Bind(fromDrafts => taggedRites.Map(fromRites => fromDrafts + fromRites));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            (await Run(stoppingToken)).Match(
                Right: tagged =>
                {
                    if (tagged > 0)
                    {
                        logger.LogInformation("Tagged {Count} rites and drafts judged before tags existed.", tagged);
                    }
                },
                Left: error => logger.LogWarning(error.ToException(), "Older rites couldn't be tagged; the next start tries again: {Reason}", error.Message));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is stopping: the next start tries again.
        }
    }

    // Stops at the first failed save; the next start picks up what's left.
    private async Task<Either<Error, int>> TagEach<T>(
        List<T> untagged, Func<T, Option<RiteSimilarity>> similarity, Func<T, string[], Task<Either<Error, Unit>>> save)
    {
        var tagged = 0;
        foreach (var item in untagged)
        {
            var tags = similarity(item).Bind(judge.TagsFrom);
            if (tags.IsNone)
            {
                continue;
            }

            var saved = await save(item, tags.IfNone([]));
            if (saved.IsLeft)
            {
                return saved.Map(_ => tagged);
            }
            tagged++;
        }
        return tagged;
    }
}
