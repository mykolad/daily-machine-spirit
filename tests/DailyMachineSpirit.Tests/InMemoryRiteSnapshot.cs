using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// The rites' snapshot kept in memory, as the blob store keeps it: each save and each discard a new version, a discarded
/// copy found by no reader. It can fail a save, hold version checks, and save another copy just before a save, as
/// another instance would.
/// </summary>
public sealed class InMemoryRiteSnapshot : IRiteSnapshotStore
{
    private int versions;
    private int checking;

    public Option<RiteSnapshot> Saved { get; private set; }

    public Option<string> CurrentVersion { get; private set; }

    /// <summary>The rites as last saved; none before the first save, or after a discard.</summary>
    public List<Rite> SavedRites => Saved.Map(saved => saved.Rites).IfNone([]);

    public int Loads { get; private set; }

    /// <summary>The most version checks that were under way at once.</summary>
    public int MostChecksAtOnce { get; private set; }

    /// <summary>The next save fails, as a storage outage would.</summary>
    public bool FailNextSave { get; set; }

    /// <summary>Runs once, just before the next save checks its version.</summary>
    public Option<Func<Task>> BeforeNextSave { get; set; }

    /// <summary>Version checks wait for this, as a slow storage account keeps them waiting.</summary>
    public Option<Task> HoldVersionChecks { get; set; }

    public async Task<Either<Error, Option<string>>> Version(CancellationToken cancellationToken)
    {
        var now = Interlocked.Increment(ref checking);
        lock (this)
        {
            MostChecksAtOnce = Math.Max(MostChecksAtOnce, now);
        }
        try
        {
            await HoldVersionChecks.IfNone(Task.CompletedTask).WaitAsync(cancellationToken);
            return CurrentVersion;
        }
        finally
        {
            Interlocked.Decrement(ref checking);
        }
    }

    public Task<Either<Error, Option<RiteSnapshot>>> Load(CancellationToken cancellationToken)
    {
        Loads++;
        return Task.FromResult(Right<Error, Option<RiteSnapshot>>(Saved));
    }

    public async Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, Option<string> replacing, CancellationToken cancellationToken)
    {
        if (FailNextSave)
        {
            FailNextSave = false;
            return Error.New("The storage account didn't answer.");
        }
        var meanwhile = BeforeNextSave;
        BeforeNextSave = None;
        await meanwhile.Match(Some: save => save(), None: () => Task.CompletedTask);
        if (CurrentVersion != replacing)
        {
            return IRiteSnapshotStore.ChangedMeanwhile;
        }
        CurrentVersion = Some($"v{++versions}");
        Saved = Some(new RiteSnapshot(rites.ToList(), CurrentVersion.IfNone(string.Empty)));
        return unit;
    }

    public Task<Either<Error, Unit>> Discard(CancellationToken cancellationToken)
    {
        CurrentVersion = Some($"v{++versions}");
        Saved = None;
        return Task.FromResult(Right<Error, Unit>(unit));
    }
}
