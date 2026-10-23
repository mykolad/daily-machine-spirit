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
        var written = await (await rites.GetAllPublished(cancellationToken))
            .BindAsync(all => store.Save(all, cancellationToken).MapAsync(_ => all.Count));
        written.IfLeft(error => logger.LogError(error.ToException(), "The rites' snapshot couldn't be written; the next write catches up: {Reason}", error.Message));
        return written;
    }
}
