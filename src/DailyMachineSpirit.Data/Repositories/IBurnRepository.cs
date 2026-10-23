using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The burned drafts that don't say who burned them, from where and why (burned before the draft kept it), and their
/// latest burn as the Record tells it. Every call returns its failure as an <see cref="Error"/>, never throws one.
/// </summary>
public interface IBurnRepository
{
    /// <summary>The burned drafts burned before the draft kept its burn's details, in no particular order.</summary>
    Task<Either<Error, List<Guid>>> GetBurnedWithoutDetails(CancellationToken cancellationToken);

    /// <summary>The draft's latest burn in the Record and the decisions; every part None when they have none.</summary>
    Task<Either<Error, RecordedBurn>> FindInTheRecord(Guid draftId, CancellationToken cancellationToken);

    /// <summary>
    /// Keeps the burn's details on the draft, if it's still burned without them: restored or burned again since, it's
    /// left as it is, and that's no failure.
    /// </summary>
    Task<Either<Error, Unit>> SaveDetails(Guid draftId, RecordedBurn burn, CancellationToken cancellationToken);
}
