using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The names the Scribes chose (#84), one document. Every change is saved with its <see cref="AuditEntry"/> in one batch.
/// Every call returns its failure as an <see cref="Error"/>, never throws one.
/// </summary>
public interface IScribeNameRepository
{
    /// <summary>Every Scribe's name, or none before the first Scribe chose one.</summary>
    Task<Either<Error, StoredScribeNames>> Get(CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="ScriptoriumRepository.ChangedMeanwhile"/> when they changed since <paramref name="read"/> was read:
    /// another Scribe chose a name in between.
    /// </summary>
    Task<Either<Error, Unit>> Save(StoredScribeNames read, List<ScribeName> changed, AuditEntry audit, CancellationToken cancellationToken);
}
