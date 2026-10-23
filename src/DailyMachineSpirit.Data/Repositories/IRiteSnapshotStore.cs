using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// A copy of every published rite kept outside Cosmos DB (#112), which the site reads from when Cosmos DB doesn't
/// answer (<see cref="RitesWithFallback"/>).
/// </summary>
public interface IRiteSnapshotStore
{
    /// <summary>Replaces the copy with these rites.</summary>
    Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, CancellationToken cancellationToken);

    /// <summary>The rites as last saved; None when no copy was ever saved, or none is kept (a local run).</summary>
    Task<Either<Error, Option<List<Rite>>>> Load(CancellationToken cancellationToken);
}
