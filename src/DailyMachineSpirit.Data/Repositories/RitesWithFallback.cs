using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The rites from Cosmos DB, or from their snapshot when Cosmos DB doesn't answer (#112): the site stays readable, its
/// pages, search and rites API, while nothing can be saved. After a failed read, the next minute's reads go to the
/// snapshot without waiting on Cosmos DB again; a read that fails the snapshot too gets Cosmos DB's error. Saving always
/// goes to Cosmos DB.
/// </summary>
public sealed class RitesWithFallback : IRiteRepository
{
    // Long enough that a page's few reads don't each wait for Cosmos DB's timeout while it's down, short enough that the
    // site writes again soon after it's back.
    private static readonly TimeSpan TrustAgainAfter = TimeSpan.FromMinutes(1);

    // The snapshot changes a few times a day; reading it once per outage, and again now and then, is enough.
    private static readonly TimeSpan SnapshotKeptFor = TimeSpan.FromMinutes(10);

    private readonly IRiteRepository rites;
    private readonly IRiteSnapshotStore snapshots;
    private readonly TimeProvider time;
    private readonly ILogger<RitesWithFallback> logger;
    private readonly object gate = new();
    private Option<DateTimeOffset> failedAt;
    private Option<(SnapshotRites Rites, DateTimeOffset LoadedAt)> loaded;

    public RitesWithFallback(IRiteRepository rites, IRiteSnapshotStore snapshots, TimeProvider time, ILogger<RitesWithFallback> logger)
    {
        this.rites = rites;
        this.snapshots = snapshots;
        this.time = time;
        this.logger = logger;
    }

    public Task<Either<Error, Option<Rite>>> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => Read(source => source.GetPublishedOn(utcDate, cancellationToken), cancellationToken);

    public Task<Either<Error, List<Rite>>> GetNewest(int count, CancellationToken cancellationToken)
        => Read(source => source.GetNewest(count, cancellationToken), cancellationToken);

    // What the snapshot is written from: never from the snapshot itself.
    public Task<Either<Error, List<Rite>>> GetAllPublished(CancellationToken cancellationToken)
        => rites.GetAllPublished(cancellationToken);

    public Task<Either<Error, List<Rite>>> GetByNumbers(IReadOnlyCollection<int> numbers, CancellationToken cancellationToken)
        => Read(source => source.GetByNumbers(numbers, cancellationToken), cancellationToken);

    public Task<Either<Error, List<Rite>>> GetOlderThan(int number, int count, CancellationToken cancellationToken)
        => Read(source => source.GetOlderThan(number, count, cancellationToken), cancellationToken);

    public Task<Either<Error, List<Rite>>> SearchOlderThan(IReadOnlyList<string> words, Option<string> tag, int number, int count, CancellationToken cancellationToken)
        => Read(source => source.SearchOlderThan(words, tag, number, count, cancellationToken), cancellationToken);

    public Task<Either<Error, List<FoundRite>>> FindDaysAndTags(IReadOnlyList<string> words, Option<string> tag, CancellationToken cancellationToken)
        => Read(source => source.FindDaysAndTags(words, tag, cancellationToken), cancellationToken);

    public Task<Either<Error, List<RiteDay>>> GetDays(CancellationToken cancellationToken)
        => Read(source => source.GetDays(cancellationToken), cancellationToken);

    public Task<Either<Error, List<RiteWords>>> GetAllWords(CancellationToken cancellationToken)
        => Read(source => source.GetAllWords(cancellationToken), cancellationToken);

    public Task<Either<Error, List<UnrevealedRite>>> GetAllUnrevealed(CancellationToken cancellationToken)
        => Read(source => source.GetAllUnrevealed(cancellationToken), cancellationToken);

    public Task<Either<Error, Option<string>>> GetHereticalTruth(int number, CancellationToken cancellationToken)
        => Read(source => source.GetHereticalTruth(number, cancellationToken), cancellationToken);

    public Task<Either<Error, Dictionary<int, float[]>>> GetScoresByRiteNumber(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => Read(source => source.GetScoresByRiteNumber(scoresGeneratorVersion, cancellationToken), cancellationToken);

    public Task<Either<Error, List<Rite>>> GetTaggedOlderThan(string tag, int number, int count, CancellationToken cancellationToken)
        => Read(source => source.GetTaggedOlderThan(tag, number, count, cancellationToken), cancellationToken);

    // Read to be saved to: only from Cosmos DB.
    public Task<Either<Error, List<Rite>>> GetUntagged(CancellationToken cancellationToken) => rites.GetUntagged(cancellationToken);

    public Task<Either<Error, Rite>> Add(Rite rite, CancellationToken cancellationToken) => rites.Add(rite, cancellationToken);

    public Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken)
        => rites.Publish(draftId, publishedOnUtc, cancellationToken);

    public Task<Either<Error, Unit>> SaveScores(DateOnly publishedOnUtc, RiteSimilarity similarity, CancellationToken cancellationToken)
        => rites.SaveScores(publishedOnUtc, similarity, cancellationToken);

    public Task<Either<Error, Unit>> SaveTags(DateOnly publishedOnUtc, string[] tags, CancellationToken cancellationToken)
        => rites.SaveTags(publishedOnUtc, tags, cancellationToken);

    private async Task<Either<Error, T>> Read<T>(Func<IRiteRepository, Task<Either<Error, T>>> read, CancellationToken cancellationToken)
    {
        var failure = Option<Error>.None;
        if (!Distrusted())
        {
            var answer = await read(rites);
            if (answer.IsRight)
            {
                return answer;
            }
            failure = answer.Swap().ToOption();
            Distrust(failure);
        }
        var snapshot = await Snapshot(cancellationToken);
        return await snapshot.Match(
            Some: read,
            None: () => Task.FromResult(failure.Match(
                Some: error => Left<Error, T>(error),
                None: () => Left<Error, T>(Error.New("Cosmos DB failed a moment ago, and the rites' snapshot can't be read.")))));
    }

    private bool Distrusted()
    {
        lock (gate)
        {
            return failedAt.Exists(at => time.GetUtcNow() - at < TrustAgainAfter);
        }
    }

    private void Distrust(Option<Error> failure)
    {
        lock (gate)
        {
            failedAt = Some(time.GetUtcNow());
        }
        failure.IfSome(error => logger.LogWarning(error.ToException(),
            "Cosmos DB didn't answer, so the rites are read from their snapshot for a minute: {Reason}", error.Message));
    }

    // The snapshot as last loaded while it's fresh, else loaded again; None when there's none to read.
    private async Task<Option<SnapshotRites>> Snapshot(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var now = time.GetUtcNow();
            var fresh = loaded.Filter(kept => now - kept.LoadedAt < SnapshotKeptFor).Map(kept => kept.Rites);
            if (fresh.IsSome)
            {
                return fresh;
            }
        }
        return (await snapshots.Load(cancellationToken)).Match(
            Right: found => found.Map(read =>
            {
                var snapshot = new SnapshotRites(read);
                lock (gate)
                {
                    loaded = Some((snapshot, time.GetUtcNow()));
                }
                return snapshot;
            }),
            Left: error =>
            {
                logger.LogError(error.ToException(), "The rites' snapshot couldn't be read either: {Reason}", error.Message);
                return None;
            });
    }
}
