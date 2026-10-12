using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>Every call returns its failure as an <see cref="Error"/>, never throws one. Publishing a draft is <see cref="IRiteRepository.Publish"/>.</summary>
public interface IDraftRepository
{
    Task<Either<Error, Draft>> Add(Draft draft, CancellationToken cancellationToken);

    /// <summary>The draft, whatever its state; None when there's no such draft.</summary>
    Task<Either<Error, Option<Draft>>> Get(Guid draftId, CancellationToken cancellationToken);

    /// <summary>The drafts that can still be published, in no particular order (the Augury orders them).</summary>
    Task<Either<Error, List<Draft>>> GetWaiting(CancellationToken cancellationToken);
}
