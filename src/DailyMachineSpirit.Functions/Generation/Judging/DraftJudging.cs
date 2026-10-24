using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation.Judging;

/// <summary>
/// Judges a waiting draft the current judge hasn't judged (judging failed, or was off, or an older judge scored it) and
/// saves the judgment, recorded (#9), if the draft is still the version judged. Each refill hands it the drafts it finds
/// (<see cref="BacklogRefiller"/>), so an outage or a new judge heals on its own; a Scribe's Judge Again hands it one. A
/// judgment says nothing about the Scribes' taste, so it's no lesson for the Augury.
/// </summary>
public sealed class DraftJudging
{
    /// <summary>Not waiting, or judged already by the current judge: there's nothing to judge.</summary>
    public static readonly Error NothingToJudge = Error.New("That rite is judged already, or no longer waits.");

    private readonly IDraftRepository drafts;
    private readonly IRiteJudge judge;
    private readonly TimeProvider time;
    private readonly ILogger<DraftJudging> logger;

    public DraftJudging(IDraftRepository drafts, IRiteJudge judge, TimeProvider time, ILogger<DraftJudging> logger)
    {
        this.drafts = drafts;
        this.judge = judge;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>
    /// The draft is read again, with its version, so a judgment never lands on words amended or judged meanwhile. None
    /// when it still has no quality (the judge failed, is off, or answered with nothing to save): nothing is saved or
    /// recorded.
    /// </summary>
    /// <param name="actor">Who asked, for the record: a Scribe's id, or the Machine Spirit.</param>
    public Task<Either<Error, Option<Augury>>> Judge(Guid draftId, Actor actor, CancellationToken cancellationToken)
        => drafts.GetWithETag(draftId, cancellationToken)
            .BindAsync(found => found
                .Filter(stored => stored.Draft.State == DraftState.Waiting && judge.NeedsJudging(stored.Draft))
                .Match(
                    Some: stored => Judged(stored.Draft, stored.ETag, actor, cancellationToken),
                    None: () => Task.FromResult(Left<Error, Option<Augury>>(NothingToJudge))));

    private async Task<Either<Error, Option<Augury>>> Judged(
        Draft draft, string etag, Actor actor, CancellationToken cancellationToken)
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

    private AuditEntry Audit(Actor actor, Draft judged, Augury augury) => new()
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
