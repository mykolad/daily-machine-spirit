using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>The day's rite, and whether this run published it (or found it already there).</summary>
public sealed record PublishedRite(Rite Rite, bool IsNew);

/// <summary>
/// Publishes the top of the Liturgical Calendar as today's (UTC) rite, unless the day already has one: running it again
/// (a retry, a missed run caught up) never replaces a rite visitors may have seen and reacted to.
/// </summary>
public sealed class DailyRitePublisher
{
    private readonly IRiteRepository rites;
    private readonly IDraftRepository drafts;
    private readonly IScriptoriumRepository scriptorium;
    private readonly BacklogRefiller refiller;
    private readonly TimeProvider time;

    public DailyRitePublisher(
        IRiteRepository rites, IDraftRepository drafts, IScriptoriumRepository scriptorium, BacklogRefiller refiller, TimeProvider time)
    {
        this.rites = rites;
        this.drafts = drafts;
        this.scriptorium = scriptorium;
        this.refiller = refiller;
        this.time = time;
    }

    public async Task<Either<Error, PublishedRite>> PublishToday(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        return await rites.GetPublishedOn(today, cancellationToken)
            .BindAsync(published => published.Match(
                Some: rite => Task.FromResult(Right<Error, PublishedRite>(new PublishedRite(rite, IsNew: false))),
                None: () => Publish(today, cancellationToken)));
    }

    private async Task<Either<Error, PublishedRite>> Publish(DateOnly today, CancellationToken cancellationToken)
    {
        var published = await NextDraft(cancellationToken)
            .BindAsync(draft => rites.Publish(draft.Id, today, cancellationToken));
        return await published.MatchAsync(
            RightAsync: rite => Task.FromResult(Right<Error, PublishedRite>(new PublishedRite(rite, IsNew: true))),
            // Another run got there first: it published today's rite (so this day has one), or it published the same
            // draft (so this one isn't waiting any more). Either way, today's rite, if it exists now, stands.
            LeftAsync: error => error == RiteRepository.DayAlreadyHasRite || error == RiteRepository.DraftNotWaiting
                ? PublishedMeanwhile(today, error, cancellationToken)
                : Task.FromResult(Left<Error, PublishedRite>(error)));
    }

    private Task<Either<Error, Draft>> NextDraft(CancellationToken cancellationToken)
        => drafts.GetWaiting(cancellationToken)
            .BindAsync(waiting => rites.GetNewest(LiturgicalCalendar.ResemblanceWindow, cancellationToken)
                .BindAsync(recent => scriptorium.GetPlacements(cancellationToken)
                    .BindAsync(placements => LiturgicalCalendar.Order(waiting, recent, placements.Order).HeadOrNone().Match(
                        Some: draft => Task.FromResult(Right<Error, Draft>(draft)),
                        // An empty backlog never leaves a day without its rite: one is written on the spot.
                        None: () => refiller.AddDraft(waiting, cancellationToken)))));

    // Theirs stands, and this run's draft (if another) keeps waiting. When the day has no rite after all (the draft was
    // published on another day), the original error stands, and the retry chooses again.
    private Task<Either<Error, PublishedRite>> PublishedMeanwhile(DateOnly day, Error error, CancellationToken cancellationToken)
        => rites.GetPublishedOn(day, cancellationToken)
            .BindAsync(published => published.Match(
                Some: rite => Right<Error, PublishedRite>(new PublishedRite(rite, IsNew: false)),
                None: () => Left<Error, PublishedRite>(error)));
}
