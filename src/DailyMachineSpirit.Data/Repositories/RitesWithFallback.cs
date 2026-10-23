using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The rites from Cosmos DB, or from their snapshot when Cosmos DB doesn't answer (#112): the site stays readable, its
/// pages, search and rites API, while nothing can be saved. After a failed read that the snapshot could answer, the next
/// minute's reads go to the snapshot without waiting on Cosmos DB again; with no snapshot, the failure is the read's, and
/// the next read tries Cosmos DB. Nothing is saved through it: what saves reads <see cref="IRiteRepository"/>.
/// </summary>
public sealed class RitesWithFallback : IRiteReader
{
    // Long enough that a page's few reads don't each wait for Cosmos DB's timeout while it's down, short enough that the
    // site writes again soon after it's back.
    private static readonly TimeSpan TrustAgainAfter = TimeSpan.FromMinutes(1);

    private readonly IRiteReader rites;
    private readonly IRiteSnapshotStore snapshots;
    private readonly TimeProvider time;
    private readonly ILogger<RitesWithFallback> logger;
    private readonly object gate = new();
    private readonly SemaphoreSlim loading = new(1, 1);
    private Option<DateTimeOffset> failedAt;
    private bool probing;
    private Option<(SnapshotRites Rites, string Version)> loaded;

    public RitesWithFallback(IRiteReader rites, IRiteSnapshotStore snapshots, TimeProvider time, ILogger<RitesWithFallback> logger)
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

    private async Task<Either<Error, T>> Read<T>(Func<IRiteReader, Task<Either<Error, T>>> read, CancellationToken cancellationToken)
    {
        var route = Route();
        if (route == Source.Copy)
        {
            return await (await Snapshot(cancellationToken)).Match(
                Some: read,
                // The copy went meanwhile: Cosmos DB, rather than nothing.
                None: () => read(rites));
        }
        try
        {
            var answer = await read(rites);
            if (answer.IsRight)
            {
                Trust();
                return answer;
            }
            var failure = answer.Match(Right: _ => Error.New("Cosmos DB failed."), Left: error => error);
            // Kept away from Cosmos DB only while there's a copy to read instead: without one, the next read tries it again.
            var snapshot = await Snapshot(cancellationToken);
            if (snapshot.IsNone)
            {
                return failure;
            }
            Distrust(failure);
            return await read(snapshot.IfNone(new SnapshotRites([])));
        }
        finally
        {
            if (route == Source.Probe)
            {
                EndProbe();
            }
        }
    }

    // Within a minute of a failure, the copy. After it, one read per instance asks Cosmos DB again (the probe) while the
    // others keep reading the copy, so a page doesn't wait on Cosmos DB's timeout each time the minute ends.
    private Source Route()
    {
        lock (gate)
        {
            if (failedAt.IsNone)
            {
                return Source.Cosmos;
            }
            if (failedAt.Exists(at => time.GetUtcNow() - at < TrustAgainAfter) || probing)
            {
                return Source.Copy;
            }
            probing = true;
            return Source.Probe;
        }
    }

    private void Trust()
    {
        lock (gate)
        {
            failedAt = None;
        }
    }

    private void Distrust(Error failure)
    {
        lock (gate)
        {
            failedAt = Some(time.GetUtcNow());
        }
        logger.LogWarning(failure.ToException(), "Cosmos DB didn't answer, so the rites are read from their snapshot for a minute: {Reason}", failure.Message);
    }

    private void EndProbe()
    {
        lock (gate)
        {
            probing = false;
        }
    }
    // The copy as kept, if it's still the version saved; else the saved one, loaded once however many reads ask at once.
    // Asking for the version costs a small request per read, only while Cosmos DB is down, made by each read at once (not
    // behind the gate): a copy saved since (an erasure's) is never missed, and only a download waits its turn.
    private async Task<Option<SnapshotRites>> Snapshot(CancellationToken cancellationToken)
    {
        var saved = (await snapshots.Version(cancellationToken)).Match(Right: version => version, Left: _ => None);
        var kept = Kept(saved);
        if (kept.IsSome)
        {
            return kept;
        }
        await loading.WaitAsync(cancellationToken);
        try
        {
            // Another read may have loaded that version while this one waited.
            kept = Kept(saved);
            if (kept.IsSome)
            {
                return kept;
            }
            return (await snapshots.Load(cancellationToken)).Match(
                Right: found =>
                {
                    var copy = found.Map(snapshot => (Rites: new SnapshotRites(snapshot.Rites), snapshot.Version));
                    lock (gate)
                    {
                        loaded = copy;
                    }
                    return copy.Map(fresh => fresh.Rites);
                },
                Left: error =>
                {
                    logger.LogError(error.ToException(), "The rites' snapshot couldn't be read either: {Reason}", error.Message);
                    return Option<SnapshotRites>.None;
                });
        }
        finally
        {
            loading.Release();
        }
    }

    private Option<SnapshotRites> Kept(Option<string> saved)
    {
        lock (gate)
        {
            return loaded.Filter(copy => saved.Exists(version => version == copy.Version)).Map(copy => copy.Rites);
        }
    }
}