using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The Precepts (#12), one document. Every change is saved with its <see cref="AuditEntry"/> in one batch, so the record
/// (#9) shows who changed them. Every call returns its failure as an <see cref="Error"/>, never throws one.
/// </summary>
public interface IPreceptRepository
{
    /// <summary>The Precepts, or an empty book before there are any.</summary>
    Task<Either<Error, StoredPrecepts>> Get(CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="ScriptoriumRepository.ChangedMeanwhile"/> when they changed since <paramref name="read"/> was read: a
    /// Scribe's edit or a distillation in between.
    /// </summary>
    Task<Either<Error, Unit>> Save(StoredPrecepts read, PreceptBook changed, AuditEntry audit, CancellationToken cancellationToken);
}
