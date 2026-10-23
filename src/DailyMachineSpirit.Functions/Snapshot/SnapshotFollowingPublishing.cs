using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Snapshot;

/// <summary>
/// Publishes as the repository it wraps does, then writes the rites' snapshot (#112) with the new rite: within the
/// publishing, so before the pages are purged, since a page purged while Cosmos DB fails is filled from it. A snapshot
/// that can't be written never fails the publishing; the next write catches up.
/// </summary>
public sealed class SnapshotFollowingPublishing : IRitePublishing
{
    private readonly IRitePublishing publishing;
    private readonly RiteSnapshots snapshots;

    public SnapshotFollowingPublishing(IRitePublishing publishing, RiteSnapshots snapshots)
    {
        this.publishing = publishing;
        this.snapshots = snapshots;
    }

    public async Task<Either<Error, Rite>> Publish(Guid draftId, DateOnly publishedOnUtc, CancellationToken cancellationToken)
    {
        var published = await publishing.Publish(draftId, publishedOnUtc, cancellationToken);
        if (published.IsRight)
        {
            await snapshots.Write(cancellationToken);
        }
        return published;
    }
}
