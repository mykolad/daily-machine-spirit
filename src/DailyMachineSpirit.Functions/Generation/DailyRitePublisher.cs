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
    private readonly BacklogRefiller refiller;
    private readonly TimeProvider time;

    public DailyRitePublisher(IRiteRepository rites, IDraftRepository drafts, BacklogRefiller refiller, TimeProvider time)
    {
        this.rites = rites;
        this.drafts = drafts;
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
            // Another run may have got there first: publishing the day's rite or this same draft, or publishing the last
            // waiting draft while this run's fallback failed to write. Whatever failed, today's rite, if it exists now,
            // stands.
            LeftAsync: error => PublishedMeanwhile(today, error, cancellationToken));
    }

    private Task<Either<Error, Draft>> NextDraft(CancellationToken cancellationToken)
        => TopOfTheCalendar(cancellationToken)
            .BindAsync(top => top.Match(
                Some: draft => Task.FromResult(Right<Error, Draft>(draft)),
                // An empty backlog never leaves a day without its rite: one is written on the spot. The calendar is read
                // again afterwards, since a refill running meanwhile may have saved a better draft.
                None: () => refiller.AddDraft([], cancellationToken)
                    .BindAsync(_ => TopOfTheCalendar(cancellationToken))
                    .BindAsync(again => again.ToEither(Error.New("The backlog is still empty after writing a draft.")))));

    private Task<Either<Error, Option<Draft>>> TopOfTheCalendar(CancellationToken cancellationToken)
        => drafts.GetWaiting(cancellationToken)
            .BindAsync(waiting => rites.GetNewest(LiturgicalCalendar.ResemblanceWindow, cancellationToken)
                .MapAsync(recent => Task.FromResult(LiturgicalCalendar.Order(waiting, recent).HeadOrNone())));

    // Theirs stands, and this run's draft (if another) keeps waiting. When the day has no rite after all, or it can't be
    // read, the original error stands, and the retry chooses again.
    private async Task<Either<Error, PublishedRite>> PublishedMeanwhile(DateOnly day, Error error, CancellationToken cancellationToken)
        => (await rites.GetPublishedOn(day, cancellationToken)).Match(
            Right: published => published.Match(
                Some: rite => Right<Error, PublishedRite>(new PublishedRite(rite, IsNew: false)),
                None: () => Left<Error, PublishedRite>(error)),
            Left: _ => Left<Error, PublishedRite>(error));
}
