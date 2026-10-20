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
    private readonly ILogger<TabCounting> logger;

    public TabCounting(
        IDraftRepository drafts, IScriptoriumRepository scriptorium, IPreceptRepository precepts, IAuditRepository audit, ILogger<TabCounting> logger)
    {
        this.drafts = drafts;
        this.scriptorium = scriptorium;
        this.precepts = precepts;
        this.audit = audit;
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
            Waiting: drafts.GetWaiting(cancellationToken),
            Burned: scriptorium.CountBurned(cancellationToken),
            Precepts: precepts.Get(cancellationToken),
            Entries: audit.Count(cancellationToken));
        var (waiting, burned, book, entries) =
            (await reads.Waiting, await reads.Burned, await reads.Precepts, await reads.Entries);

        var counts =
            from rites in waiting
            from ashes in burned
            from stored in book
            from recorded in entries
            select new TabCounts(rites.Count, ashes, stored.Book.Precepts.Count, recorded);
        return counts.Match(
            Right: Some,
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The Scriptorium's tabs couldn't be counted: {Reason}", error.Message);
                return Option<TabCounts>.None;
            });
    }
}
