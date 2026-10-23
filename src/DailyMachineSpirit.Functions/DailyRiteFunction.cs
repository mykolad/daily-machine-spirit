using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Snapshot;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions;

public sealed class DailyRiteFunction
{
    // Midnight UTC, when a rite's day begins. The host remembers the last run (schedule monitoring), so a run missed while
    // the app was down happens as soon as it's back.
    public const string Schedule = "0 0 0 * * *";

    private readonly DailyRitePublisher publisher;
    private readonly IPageCache pages;
    private readonly RiteSnapshots snapshots;
    private readonly ILogger<DailyRiteFunction> logger;

    public DailyRiteFunction(DailyRitePublisher publisher, IPageCache pages, RiteSnapshots snapshots, ILogger<DailyRiteFunction> logger)
    {
        this.publisher = publisher;
        this.pages = pages;
        this.snapshots = snapshots;
        this.logger = logger;
    }

    /// <summary>Publishes today's rite, then asks for the backlog to be refilled (the message is the reason).</summary>
    // A failed run (every model failing on an empty backlog, Cosmos down) is tried again later in the day; publishing
    // skips a day that already has its rite, so a retry is always safe.
    [Function("DailyRite")]
    [FixedDelayRetry(3, "00:20:00")]
    [QueueOutput(RefillBacklogFunction.QueueName)]
    public async Task<string> Run([TimerTrigger(Schedule)] TimerInfo timer, CancellationToken cancellationToken)
    {
        var result = await publisher.PublishToday(cancellationToken);
        if (result.Exists(published => published.IsNew))
        {
            // Today showed yesterday's rite (or the slumbering page), and the search field's index and the rites API
            // lacked the new one: all are kept at the edge until purged.
            await pages.Purge([Paths.Today, Paths.SearchIndex, Paths.RitesApi], cancellationToken);
            // The fallback's copy (#112) shows the new rite too.
            await snapshots.Write(cancellationToken);
        }
        return result.Match(
            Right: published =>
            {
                logger.LogInformation("Rite NO. {Number} for {Day}: {Outcome}.",
                    published.Rite.Number, published.Rite.PublishedOnUtc, published.IsNew ? "published now" : "already published");
                return RefillBacklogFunction.AfterPublishing;
            },
            // Logged here, since only the app's telemetry is exported, then thrown, so the host counts the run as failed
            // (and retries it).
            Left: error =>
            {
                logger.LogError(error.ToException(), "Today's rite couldn't be published: {Reason}", error.Message);
                throw error.ToException();
            });
    }
}
