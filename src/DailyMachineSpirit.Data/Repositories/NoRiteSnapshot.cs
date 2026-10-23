using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>No snapshot kept (a local run, or a test): saving does nothing, and there's nothing to fall back on.</summary>
public sealed class NoRiteSnapshot : IRiteSnapshotStore
{
    public Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, CancellationToken cancellationToken)
        => Task.FromResult(Right<Error, Unit>(unit));

    public Task<Either<Error, Option<List<Rite>>>> Load(CancellationToken cancellationToken)
        => Task.FromResult(Right<Error, Option<List<Rite>>>(None));
}
