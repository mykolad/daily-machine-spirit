using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// Judges again the waiting drafts judging couldn't score (Jev failed, or was off): each refill does, before it writes,
/// so an outage heals on its own, and a Scribe can ask for one in the Scriptorium. A judgment says nothing about the
/// Scribes' taste, so it's recorded (#9) but no lesson for the Augury.
/// </summary>
public sealed class JudgingAgain
{
    /// <summary>Not waiting, or judged already: there's nothing to judge again.</summary>
    public static readonly Error NothingToJudge = Error.New("That rite is judged already, or no longer waits.");

    private readonly IDraftRepository drafts;
    private readonly IRiteJudge judge;
    private readonly TimeProvider time;
    private readonly ILogger<JudgingAgain> logger;

    public JudgingAgain(IDraftRepository drafts, IRiteJudge judge, TimeProvider time, ILogger<JudgingAgain> logger)
    {
        this.drafts = drafts;
        this.judge = judge;
        this.time = time;
        this.logger = logger;
    }

    /// <param name="actor">Who asked, for the record: a Scribe's id, or the Machine Spirit.</param>
    public Task<Either<Error, Option<Augury>>> JudgeAgain(Guid draftId, string actor, CancellationToken cancellationToken)
        => drafts.GetWithETag(draftId, cancellationToken)
            .BindAsync(found => found
                .Filter(stored => stored.Draft.State == DraftState.Waiting && stored.Draft.Augury.IsNone)
                .Match(
                    Some: stored => Judged(stored.Draft, stored.ETag, actor, cancellationToken),
                    None: () => Task.FromResult(Left<Error, Option<Augury>>(NothingToJudge))));

    // As the Machine Spirit: how many it judged now.
    public Task<Either<Error, int>> JudgeTheUnjudged(CancellationToken cancellationToken)
        => drafts.GetWaiting(cancellationToken)
            .BindAsync(async waiting =>
            {
                var judged = 0;
                foreach (var draft in waiting.Where(draft => draft.Augury.IsNone))
                {
                    var result = await JudgeAgain(draft.Id, AuditEntry.MachineSpirit, cancellationToken);
                    if (result.Exists(augury => augury.IsSome))
                    {
                        judged++;
                    }
                    result.IfLeft(error => LogUnlessChangedMeanwhile(draft, error));
                }
                return Right<Error, int>(judged);
            });

    // Still without a quality (the judge is off, or answered with nothing to save), nothing is saved and nothing recorded.
    private async Task<Either<Error, Option<Augury>>> Judged(
        Draft draft, string etag, string actor, CancellationToken cancellationToken)
        => await (await judge.Judge(draft, cancellationToken)).Match(
            Right: judged => judged.Augury.Match(
                Some: augury => drafts.SaveJudgmentIfUnchanged(judged, etag, Audit(actor, judged, augury), cancellationToken)
                    .MapAsync(_ => Task.FromResult(Some(augury))),
                None: () => Task.FromResult(Right<Error, Option<Augury>>(None))),
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "Draft {Title} still can't be judged: {Reason}",
                    draft.Writing.Title, error.Message);
                return Task.FromResult(Right<Error, Option<Augury>>(None));
            });

    // Amended, burned, published or judged since the refill listed it: no failure, just nothing left to judge.
    private void LogUnlessChangedMeanwhile(Draft draft, Error error)
    {
        if (error != ScriptoriumRepository.ChangedMeanwhile && error != NothingToJudge)
        {
            logger.LogWarning(error.ToException(), "Draft {Title} couldn't be judged again: {Reason}",
                draft.Writing.Title, error.Message);
        }
    }

    private AuditEntry Audit(string actor, Draft judged, Augury augury) => new()
    {
        Id = Guid.NewGuid(),
        AtUtc = time.GetUtcNow().UtcDateTime,
        Actor = actor,
        Action = AuditAction.JudgeAgain,
        DraftId = judged.Id,
        Title = judged.Writing.Title,
        Detail = Some($"the Augury: {augury.Quality:P0}"),
    };
}
