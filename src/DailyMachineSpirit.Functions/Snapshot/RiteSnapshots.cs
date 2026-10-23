using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Snapshot;

/// <summary>
/// Writes the rites' snapshot (#112) from Cosmos DB: after a new rite and an erasure, which change what the pages show,
/// and every few hours for the rest (votes, tags, ailments). A failure is logged and never fails what asked for it: the
/// next write catches up.
/// </summary>
public sealed class RiteSnapshots
{
    // Each try reads the rites again: a copy saved meanwhile (an erasure's) may be newer than what this one read.
    private const int Tries = 3;

    // How long following an erasure may take, whatever the request that erased does meanwhile: the rewrite, then a
    // discard with time of its own, so a rewrite that runs out of time still leaves no copy holding the erased words.
    private static readonly TimeSpan RewriteTime = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DiscardTime = TimeSpan.FromSeconds(10);

    private readonly IRiteRepository rites;
    private readonly IRiteSnapshotStore store;
    private readonly TimeProvider time;
    private readonly ILogger<RiteSnapshots> logger;

    public RiteSnapshots(IRiteRepository rites, IRiteSnapshotStore store, TimeProvider time, ILogger<RiteSnapshots> logger)
    {
        this.rites = rites;
        this.store = store;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>How many rites the snapshot now holds.</summary>
    public async Task<Either<Error, int>> Write(CancellationToken cancellationToken)
    {
        var written = await WriteOnce(cancellationToken);
        for (var tried = 1; tried < Tries && written.Match(Right: _ => false, Left: error => error == IRiteSnapshotStore.ChangedMeanwhile); tried++)
        {
            written = await WriteOnce(cancellationToken);
        }
        written.IfLeft(error => logger.LogError(error.ToException(), "The rites' snapshot couldn't be written; the next write catches up: {Reason}", error.Message));
        return written;
    }

    /// <summary>
    /// Writes the snapshot after an erasure, or if that fails, takes the copy away: an old copy would hold the erased words,
    /// which a fallback would show again. Without a copy there's no fallback until the next write (every six hours). It
    /// runs to the end even if the request that erased is cancelled meanwhile: the erasure is saved already.
    /// </summary>
    public async Task WriteOrDiscard()
    {
        if ((await Within(RewriteTime, Write)).IsRight)
        {
            return;
        }
        (await Within(DiscardTime, store.Discard)).IfLeft(error => logger.LogError(error.ToException(),
            "The rites' snapshot couldn't be written after an erasure, nor taken away: it may hold the erased words until the next write: {Reason}",
            error.Message));
    }

    // Running out of time is a failure like any other here, not a cancellation to pass on: nobody asked to stop.
    private async Task<Either<Error, T>> Within<T>(TimeSpan limit, Func<CancellationToken, Task<Either<Error, T>>> step)
    {
        using var timeout = new CancellationTokenSource(limit, time);
        try
        {
            return await step(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return Error.New($"Following the erasure took longer than {limit.TotalSeconds} seconds.");
        }
    }

    // The copy's version is read before the rites, so a copy saved after this read the rites is never replaced by it.
    private async Task<Either<Error, int>> WriteOnce(CancellationToken cancellationToken)
        => await (await store.Version(cancellationToken)).BindAsync(async versionRead =>
            await (await rites.GetAllPublished(cancellationToken))
                .BindAsync(all => store.SaveIfStillVersion(all, versionRead, cancellationToken).MapAsync(_ => all.Count)));
}
