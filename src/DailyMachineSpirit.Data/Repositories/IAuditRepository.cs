using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The record of changes (#9). Most entries are saved in the batch of the change they record (the Scriptorium's
/// changes, a written draft, a published rite); <see cref="Add"/> is for an action that changes nothing else stored.
/// Every call returns its failure as an <see cref="Error"/>, never throws one.
/// </summary>
public interface IAuditRepository
{
    Task<Either<Error, Unit>> Add(AuditEntry entry, CancellationToken cancellationToken);

    /// <summary>The newest entries first: all of them, or one draft's.</summary>
    Task<Either<Error, List<AuditEntry>>> GetNewest(int count, Option<Guid> draftId, CancellationToken cancellationToken);
}
