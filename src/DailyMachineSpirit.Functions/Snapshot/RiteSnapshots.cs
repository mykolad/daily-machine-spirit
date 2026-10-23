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

    private readonly IRiteRepository rites;
    private readonly IRiteSnapshotStore store;
    private readonly ILogger<RiteSnapshots> logger;

    public RiteSnapshots(IRiteRepository rites, IRiteSnapshotStore store, ILogger<RiteSnapshots> logger)
    {
        this.rites = rites;
        this.store = store;
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

    // The copy's version is read before the rites, so a copy saved after this read the rites is never replaced by it.
    private async Task<Either<Error, int>> WriteOnce(CancellationToken cancellationToken)
        => await (await store.Version(cancellationToken)).BindAsync(async replacing =>
            await (await rites.GetAllPublished(cancellationToken))
                .BindAsync(all => store.Save(all, replacing, cancellationToken).MapAsync(_ => all.Count)));
}
