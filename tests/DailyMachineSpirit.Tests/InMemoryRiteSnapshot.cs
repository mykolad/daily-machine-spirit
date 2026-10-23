using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// The rites' snapshot kept in memory, each save a new version: what was saved last, how many times it was loaded, and a
/// way to save another copy just before a save, as another instance would.
/// </summary>
public sealed class InMemoryRiteSnapshot : IRiteSnapshotStore
{
    private int saves;

    public Option<RiteSnapshot> Saved { get; private set; }

    /// <summary>The rites as last saved; none before the first save.</summary>
    public List<Rite> SavedRites => Saved.Map(saved => saved.Rites).IfNone([]);

    public int Loads { get; private set; }

    /// <summary>Runs once, just before the next save checks its version.</summary>
    public Option<Func<Task>> BeforeNextSave { get; set; }

    public Task<Either<Error, Option<string>>> Version(CancellationToken cancellationToken)
        => Task.FromResult(Right<Error, Option<string>>(Saved.Map(saved => saved.Version)));

    public Task<Either<Error, Option<RiteSnapshot>>> Load(CancellationToken cancellationToken)
    {
        Loads++;
        return Task.FromResult(Right<Error, Option<RiteSnapshot>>(Saved));
    }

    public async Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, Option<string> replacing, CancellationToken cancellationToken)
    {
        var meanwhile = BeforeNextSave;
        BeforeNextSave = None;
        await meanwhile.Match(Some: save => save(), None: () => Task.CompletedTask);
        if (Saved.Map(saved => saved.Version) != replacing)
        {
            return IRiteSnapshotStore.ChangedMeanwhile;
        }
        Saved = Some(new RiteSnapshot(rites.ToList(), $"v{++saves}"));
        return unit;
    }
}
