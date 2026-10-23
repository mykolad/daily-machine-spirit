using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>No snapshot kept (a local run): saving does nothing, and there's nothing to fall back on.</summary>
public sealed class NoRiteSnapshot : IRiteSnapshotStore
{
    public Task<Either<Error, Option<string>>> Version(CancellationToken cancellationToken)
        => Task.FromResult(Right<Error, Option<string>>(None));

    public Task<Either<Error, Option<RiteSnapshot>>> Load(CancellationToken cancellationToken)
        => Task.FromResult(Right<Error, Option<RiteSnapshot>>(None));

    public Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, Option<string> replacing, CancellationToken cancellationToken)
        => Task.FromResult(Right<Error, Unit>(unit));
}
