using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// What the Scribes change: their order of the drafts, and which drafts are burned. Each change is saved in one batch
/// with the <see cref="ScribeDecision"/> that records it and its <see cref="AuditEntry"/>, so neither the lessons for the
/// Augury nor the record (#9) can miss a change or invent one.
/// Every call returns its failure as an <see cref="Error"/>, never throws one.
/// </summary>
public interface IScriptoriumRepository
{
    Task<Either<Error, Placements>> GetPlacements(CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="ScriptoriumRepository.ChangedMeanwhile"/> when another Scribe saved an order since it was read, or when
    /// any of <paramref name="mustStillWait"/> isn't waiting any more (burned or published meanwhile): a move places
    /// the drafts it passes as well as the one moved, and an order built on a draft that's gone would mislead.
    /// </summary>
    Task<Either<Error, Unit>> SavePlacements(
        Placements placements, Option<ScribeDecision> decision, IReadOnlyCollection<Guid> mustStillWait, AuditEntry audit, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a draft from one state to another (burning, restoring), and saves <paramref name="placements"/> with it when
    /// given (taking the draft out of the Scribes' order). <see cref="ScriptoriumRepository.ChangedMeanwhile"/> when the
    /// draft isn't in <paramref name="from"/> any more, or another Scribe changed the order.
    /// </summary>
    Task<Either<Error, Draft>> ChangeState(
        Guid draftId,
        DraftState from,
        DraftState to,
        ScribeDecision decision,
        Option<Placements> placements,
        AuditEntry audit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves the amended draft with the decision that records it, if the draft is still the version read
    /// (<paramref name="etag"/>, from <see cref="IDraftRepository.GetWithETag"/>): else
    /// <see cref="ScriptoriumRepository.ChangedMeanwhile"/>, as when another Scribe amended, burned or published it since.
    /// </summary>
    Task<Either<Error, Unit>> Amend(Draft amended, string etag, ScribeDecision decision, AuditEntry audit, CancellationToken cancellationToken);

    /// <summary>How many drafts are burned, however long ago.</summary>
    Task<Either<Error, int>> CountBurned(CancellationToken cancellationToken);

    /// <summary>Burned drafts, the most recently burned first.</summary>
    Task<Either<Error, List<Draft>>> GetBurned(int count, CancellationToken cancellationToken);

    /// <summary>The newest decisions first.</summary>
    Task<Either<Error, List<ScribeDecision>>> GetDecisions(int count, CancellationToken cancellationToken);

    /// <summary>Every decision made on or after the day (UTC), in no particular order.</summary>
    Task<Either<Error, List<ScribeDecision>>> GetDecisionsSince(DateOnly day, CancellationToken cancellationToken);
}
