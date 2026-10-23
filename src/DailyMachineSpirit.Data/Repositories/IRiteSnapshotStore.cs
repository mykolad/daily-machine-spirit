using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// A copy of every published rite kept outside Cosmos DB (#112), which the site reads from when Cosmos DB doesn't
/// answer (<see cref="RitesWithFallback"/>). Each saved copy has a version, so a copy taken earlier can't replace one
/// taken later, and a reader can tell its copy is old.
/// </summary>
public interface IRiteSnapshotStore
{
    /// <summary>Another copy was saved since <c>replacing</c> was read: what was read to save may be older than it.</summary>
    static readonly Error ChangedMeanwhile = Error.New("The rites' snapshot was saved again meanwhile.");

    /// <summary>The version of the copy as last saved; None when none was ever saved, or none is kept.</summary>
    Task<Either<Error, Option<string>>> Version(CancellationToken cancellationToken);

    /// <summary>The copy as last saved, with its version.</summary>
    Task<Either<Error, Option<RiteSnapshot>>> Load(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the copy with these rites, only if it's still the version read before reading them (None: there was
    /// none), else <see cref="ChangedMeanwhile"/>.
    /// </summary>
    Task<Either<Error, Unit>> Save(IReadOnlyList<Rite> rites, Option<string> replacing, CancellationToken cancellationToken);

    /// <summary>Takes the copy away, so nothing falls back on it until the next save.</summary>
    Task<Either<Error, Unit>> Discard(CancellationToken cancellationToken);
}
