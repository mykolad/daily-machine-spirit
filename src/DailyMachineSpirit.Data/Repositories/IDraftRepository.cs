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

    /// <summary>
    /// The draft and its version (ETag), for an amendment: saving it fails if anything changed the draft since
    /// (<see cref="IScriptoriumRepository.Amend"/>). None when there's no such draft.
    /// </summary>
    Task<Either<Error, Option<(Draft Draft, string ETag)>>> GetWithETag(Guid draftId, CancellationToken cancellationToken);

    /// <summary>The drafts that can still be published, in no particular order (the Augury orders them).</summary>
    Task<Either<Error, List<Draft>>> GetWaiting(CancellationToken cancellationToken);

    /// <summary>The drafts judged before tags existed: with scores, without tags. Whatever their state.</summary>
    Task<Either<Error, List<Draft>>> GetUntagged(CancellationToken cancellationToken);

    Task<Either<Error, Unit>> SaveTags(Guid draftId, string[] tags, CancellationToken cancellationToken);
}
