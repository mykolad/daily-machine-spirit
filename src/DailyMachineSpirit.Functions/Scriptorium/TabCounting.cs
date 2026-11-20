using DailyMachineSpirit.Data.Repositories;
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
    private readonly ILogger<TabCounting> logger;

    public TabCounting(
        IDraftRepository drafts, IScriptoriumRepository scriptorium, IPreceptRepository precepts, IAuditRepository audit,
        IFalseTruthsRepository falseTruths, ILogger<TabCounting> logger)
    {
        this.drafts = drafts;
        this.scriptorium = scriptorium;
        this.precepts = precepts;
        this.audit = audit;
        this.falseTruths = falseTruths;
        this.logger = logger;
    }

    /// <summary>
    /// None when any count can't be read: the counts are a nicety, so the tabs then go without them and the page still
    /// shows.
    /// </summary>
    public async Task<Option<TabCounts>> Count(CancellationToken cancellationToken)
    {
        // Asked all at once, then gathered.
        var reads = (
            Placements: scriptorium.GetPlacements(cancellationToken),
            Waiting: drafts.GetWaiting(cancellationToken),
            Burned: scriptorium.CountBurned(cancellationToken),
            Precepts: precepts.Get(cancellationToken),
            Entries: audit.Count(cancellationToken),
            Proposals: falseTruths.Proposals(cancellationToken));
        var (placements, waiting, burned, book, entries, proposals) =
            (await reads.Placements, await reads.Waiting, await reads.Burned, await reads.Precepts, await reads.Entries, await reads.Proposals);

        var counts =
            from order in placements
            from rites in waiting
            from ashes in burned
            from stored in book
            from recorded in entries
            from proposed in proposals
            let placed = rites.Count(draft => order.Order.Contains(draft.Id))
            select new TabCounts(placed, rites.Count - placed, ashes, stored.Book.Precepts.Count, recorded, proposed.Count);
        return counts.Match(
            Right: Some,
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The Scriptorium's tabs couldn't be counted: {Reason}", error.Message);
                return Option<TabCounts>.None;
            });
    }
}
