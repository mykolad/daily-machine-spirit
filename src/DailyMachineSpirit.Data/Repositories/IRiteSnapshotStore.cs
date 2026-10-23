using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>A copy of every published rite kept outside Cosmos DB (#112), read while Cosmos DB doesn't answer.</summary>
public interface IRiteSnapshotStore
{
    static readonly Error ChangedMeanwhile = Error.New("The rites' snapshot was saved again meanwhile.");

    Task<Either<Error, Option<string>>> Version(CancellationToken cancellationToken);

    Task<Either<Error, Option<RiteSnapshot>>> Load(CancellationToken cancellationToken);

    Task<Either<Error, Unit>> SaveIfStillVersion(IReadOnlyList<Rite> rites, Option<string> versionRead, CancellationToken cancellationToken);

    Task<Either<Error, Unit>> Discard(CancellationToken cancellationToken);
}
