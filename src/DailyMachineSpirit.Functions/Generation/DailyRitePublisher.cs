using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Scoring;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>
/// Publishes today's (UTC) rite unless the day already has one, so running it again (a retry, a missed run caught up)
/// never replaces a rite visitors may have seen and reacted to.
/// </summary>
public sealed class DailyRitePublisher
{
    private readonly IRiteRepository rites;
    private readonly RiteWriter writer;
    private readonly IRiteScorer scorer;
    private readonly IOptions<GenerationOptions> options;
    private readonly TimeProvider time;
    private readonly SiteMetrics metrics;
    private readonly ILogger<DailyRitePublisher> logger;

    public DailyRitePublisher(
        IRiteRepository rites,
        RiteWriter writer,
        IRiteScorer scorer,
        IOptions<GenerationOptions> options,
        TimeProvider time,
        SiteMetrics metrics,
        ILogger<DailyRitePublisher> logger)
    {
        this.rites = rites;
        this.writer = writer;
        this.scorer = scorer;
        this.options = options;
        this.time = time;
        this.metrics = metrics;
        this.logger = logger;
    }

    /// <summary>Prayers and rituals take turns. It goes by the date, so a day's kind never depends on what ran before.</summary>
    public static RiteKind KindFor(DateOnly day) => day.DayNumber % 2 == 0 ? RiteKind.Prayer : RiteKind.Ritual;

    public async Task<Either<Error, PublishedRite>> PublishToday(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return await rites.GetPublishedOn(today, cancellationToken)
            .BindAsync(published => published.Match(
                Some: rite => Task.FromResult(Right<Error, PublishedRite>(new PublishedRite(rite, IsNew: false))),
                None: () => Publish(today, cancellationToken)));
    }

    private Task<Either<Error, PublishedRite>> Publish(DateOnly today, CancellationToken cancellationToken)
        => rites.GetNewest(options.Value.RecentRitesInPrompt, cancellationToken)
            .BindAsync(recent => writer.Write(today, KindFor(today), recent.Select(rite => rite.Title).ToList(), cancellationToken))
            .BindAsync(rite => Save(rite, cancellationToken));

    private async Task<Either<Error, PublishedRite>> Save(Rite rite, CancellationToken cancellationToken)
    {
        var saved = await rites.Add(rite, cancellationToken);
        return await saved.MatchAsync(
            RightAsync: async added =>
            {
                metrics.Published(added);
                return Right<Error, PublishedRite>(new PublishedRite(await WithScores(added, cancellationToken), IsNew: true));
            },
            LeftAsync: error => error == RiteRepository.DayAlreadyHasRite
                ? PublishedMeanwhile(rite.PublishedOnUtc, cancellationToken)
                : Task.FromResult(Left<Error, PublishedRite>(error)));
    }

    // Another run saved the day's rite while this one was writing: theirs stands, and this one's is dropped.
    private Task<Either<Error, PublishedRite>> PublishedMeanwhile(DateOnly day, CancellationToken cancellationToken)
        => rites.GetPublishedOn(day, cancellationToken)
            .BindAsync(published => published.Match(
                Some: rite => Right<Error, PublishedRite>(new PublishedRite(rite, IsNew: false)),
                None: () => Left<Error, PublishedRite>(Error.New($"{day:yyyy-MM-dd} has a rite, but it can't be found."))));

    // Scores only power "More rites": a rite without them is still today's rite, so failing to score never fails the
    // publishing. The rite simply has no related rites until it's scored.
    private async Task<Rite> WithScores(Rite rite, CancellationToken cancellationToken)
    {
        var scored = await scorer.Score(rite, cancellationToken)
            .BindAsync(similarity => similarity.Match(
                Some: async scores => (await rites.SaveScores(rite.PublishedOnUtc, scores, cancellationToken))
                    .Map(_ =>
                    {
                        logger.LogInformation("Rite NO. {Number} was scored by {ScoresGenerator}.", rite.Number, scores.ScoresGeneratorVersion);
                        metrics.Scoring("scored");
                        return rite with { Similarity = scores };
                    }),
                None: () =>
                {
                    logger.LogInformation("Rite NO. {Number} was published without scores: scoring is turned off.", rite.Number);
                    metrics.Scoring("off");
                    return Task.FromResult(Right<Error, Rite>(rite));
                }));
        return scored.IfLeft(error =>
        {
            logger.LogWarning(error.ToException(), "Rite NO. {Number} was published without scores: {Reason}", rite.Number, error.Message);
            metrics.Scoring("failed");
            return rite;
        });
    }
}
