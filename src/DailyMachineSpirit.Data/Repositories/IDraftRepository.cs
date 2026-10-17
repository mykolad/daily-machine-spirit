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

    /// <summary>
    /// Tags a draft <see cref="GetUntagged"/> found, if it's still untagged and scored: changed since (an amendment
    /// tagged it anew, or cleared its scores), it's left as it is.
    /// </summary>
    Task<Either<Error, Unit>> SaveTags(Guid draftId, string[] tags, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a judgment (scores, quality, tags) with its record entry, if the draft is still the version judged
    /// (<paramref name="etag"/>): <see cref="ScriptoriumRepository.ChangedMeanwhile"/> when it was judged, amended, burned
    /// or published since, so a judgment of old words never lands on new ones.
    /// </summary>
    Task<Either<Error, Unit>> SaveJudgmentIfUnchanged(Draft judged, string etag, AuditEntry audit, CancellationToken cancellationToken);
}
