using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The rites' snapshot kept in memory: what was saved last, and how many times it was saved or loaded.</summary>
public sealed class InMemoryRiteSnapshot : IRiteSnapshotStore
{
    public Option<List<Rite>> Saved { get; private set; }

    public int Loads { get; private set; }

    public Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, CancellationToken cancellationToken)
    {
        Saved = Some(rites.ToList());
        return Task.FromResult(Right<Error, Unit>(unit));
    }

    public Task<Either<Error, Option<List<Rite>>>> Load(CancellationToken cancellationToken)
    {
        Loads++;
        return Task.FromResult(Right<Error, Option<List<Rite>>>(Saved));
    }
}
