using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation;
using LanguageExt;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>Counts what each of the Scriptorium's tabs holds, for its tab bar.</summary>
public sealed class TabCounting
{
    private readonly IDraftRepository drafts;
    private readonly IScriptoriumRepository scriptorium;
    private readonly IPreceptRepository precepts;
    private readonly IAuditRepository audit;
    private readonly IFalseTruthsRepository falseTruths;
    private readonly IRiteRepository rites;
    private readonly TimeProvider time;
    private readonly ILogger<TabCounting> logger;

    public TabCounting(
        IDraftRepository drafts, IScriptoriumRepository scriptorium, IPreceptRepository precepts, IAuditRepository audit,
        IFalseTruthsRepository falseTruths, IRiteRepository rites, TimeProvider time, ILogger<TabCounting> logger)
    {
        this.drafts = drafts;
        this.scriptorium = scriptorium;
        this.precepts = precepts;
        this.audit = audit;
        this.falseTruths = falseTruths;
        this.rites = rites;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>
    /// None when any count can't be read: the counts are a nicety, so the tabs then go without them and the page still
    /// shows.
    /// </summary>
    public async Task<Option<TabCounts>> Count(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        // Asked all at once, then gathered.
        var reads = (
            Placements: scriptorium.GetPlacements(cancellationToken),
            Waiting: drafts.GetWaiting(cancellationToken),
            Burned: scriptorium.CountBurned(cancellationToken),
            Precepts: precepts.Get(cancellationToken),
            Entries: audit.Count(cancellationToken),
            Proposals: falseTruths.Proposals(cancellationToken),
            Todays: rites.GetPublishedOn(today, cancellationToken));
        var (placements, waiting, burned, book, entries, proposals, todays) =
            (await reads.Placements, await reads.Waiting, await reads.Burned, await reads.Precepts, await reads.Entries, await reads.Proposals, await reads.Todays);

        var counts =
            from order in placements
            from waitingDrafts in waiting
            from ashes in burned
            from stored in book
            from recorded in entries
            from proposed in proposals
            from published in todays
            // As the Calendar groups them: a feast's own draft (#202) by the same rules as the publisher's, the rest in the orders.
            let feasts = PublishingDays.Feasts(waitingDrafts, published.IsSome ? today.AddDays(1) : today).Values.Select(draft => draft.Id).ToHashSet()
            let placed = waitingDrafts.Count(draft => !feasts.Contains(draft.Id) && order.Order.Contains(draft.Id))
            select new TabCounts(feasts.Count, placed, waitingDrafts.Count - feasts.Count - placed, ashes, stored.Book.Precepts.Count, recorded, proposed.Count);
        return counts.Match(
            Right: Some,
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The Scriptorium's tabs couldn't be counted: {Reason}", error.Message);
                return Option<TabCounts>.None;
            });
    }
}
