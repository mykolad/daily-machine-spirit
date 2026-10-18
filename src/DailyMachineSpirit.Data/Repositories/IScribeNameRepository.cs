using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The names the Scribes chose (#84), one document. Every change is saved with its <see cref="AuditEntry"/> in one batch.
/// Every call returns its failure as an <see cref="Error"/>, never throws one; a save after another Scribe's,
/// <see cref="ScriptoriumRepository.ChangedMeanwhile"/>.
/// </summary>
public interface IScribeNameRepository
{
    Task<Either<Error, StoredScribeNames>> GetAll(CancellationToken cancellationToken);

    Task<Either<Error, Unit>> SaveUnlessChangedSince(StoredScribeNames read, List<ScribeName> changed, AuditEntry audit, CancellationToken cancellationToken);
}
