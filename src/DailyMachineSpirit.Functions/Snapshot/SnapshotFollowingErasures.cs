using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Snapshot;

/// <summary>
/// Erases as the repository it wraps does, then writes the rites' snapshot (#112) without the erased words, or discards
/// it: within the erasure, so before its pages are purged, since a page purged while Cosmos DB fails is filled from it.
/// </summary>
public sealed class SnapshotFollowingErasures : IExpurgationRepository
{
    private readonly IExpurgationRepository copies;
    private readonly RiteSnapshots snapshots;

    public SnapshotFollowingErasures(IExpurgationRepository copies, RiteSnapshots snapshots)
    {
        this.copies = copies;
        this.snapshots = snapshots;
    }

    public Task<Either<Error, Option<KeptWords>>> FindWordsOf(int number, CancellationToken cancellationToken)
        => copies.FindWordsOf(number, cancellationToken);

    public async Task<Either<Error, Unit>> Erase(IReadOnlyList<WordsKept> changed, AuditEntry audit, CancellationToken cancellationToken)
    {
        var erased = await copies.Erase(changed, audit, cancellationToken);
        if (erased.IsRight)
        {
            await snapshots.WriteOrDiscard();
        }
        return erased;
    }
}
