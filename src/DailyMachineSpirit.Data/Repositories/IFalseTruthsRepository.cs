using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// The backfill of false truths (#199) for what was written before them: published rites get them proposed, for a Scribe
/// to accept; waiting drafts get them as a new draft would, since the Scribes read every draft before it's published.
/// </summary>
public interface IFalseTruthsRepository
{
    /// <summary>The published rites with neither false truths nor a proposal, by number.</summary>
    Task<Either<Error, IReadOnlyList<int>>> RitesWithout(CancellationToken cancellationToken);

    /// <summary>The waiting drafts without false truths.</summary>
    Task<Either<Error, IReadOnlyList<Guid>>> DraftsWithout(CancellationToken cancellationToken);

    /// <summary>
    /// Proposes false truths for a rite that has neither them nor a proposal; Some(false) when it has either by now (an
    /// earlier message, a Scribe), None when there's no such rite.
    /// </summary>
    Task<Either<Error, Option<bool>>> Propose(int number, FalseTruths proposed, CancellationToken cancellationToken);

    /// <summary>Gives a waiting draft without false truths these; false when it isn't one by now (published, amended).</summary>
    Task<Either<Error, bool>> GiveDraft(Guid draftId, FalseTruths falseTruths, CancellationToken cancellationToken);

    /// <summary>The proposals waiting for a Scribe, oldest rite first.</summary>
    Task<Either<Error, IReadOnlyList<ProposedFalseTruths>>> Proposals(CancellationToken cancellationToken);

    /// <summary>
    /// The rite takes these false truths (the proposal, or the Scribe's words for it) and the proposal goes; false when
    /// it has no proposal by now (accepted in another tab).
    /// </summary>
    Task<Either<Error, bool>> Accept(int number, FalseTruths accepted, CancellationToken cancellationToken);

    /// <summary>Drops the rite's proposal, so it's written again; false when it had none.</summary>
    Task<Either<Error, bool>> Withdraw(int number, CancellationToken cancellationToken);
}
