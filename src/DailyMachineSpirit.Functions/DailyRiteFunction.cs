using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Functions;

public sealed class DailyRiteFunction
{
    // Midnight UTC, when a rite's day begins. The host remembers the last run (schedule monitoring), so a run missed while
    // the app was down happens as soon as it's back.
    public const string Schedule = "0 0 0 * * *";

    private readonly DailyRitePublisher publisher;
    private readonly IPageCache pages;
    private readonly IOptions<GenerationOptions> options;
    private readonly ILogger<DailyRiteFunction> logger;

    public DailyRiteFunction(DailyRitePublisher publisher, IPageCache pages, IOptions<GenerationOptions> options, ILogger<DailyRiteFunction> logger)
    {
        this.publisher = publisher;
        this.pages = pages;
        this.options = options;
        this.logger = logger;
    }

    /// <summary>
    /// Publishes today's rite, then asks for the backlog to be refilled (the message is the reason). With
    /// <see cref="GenerationOptions.PublishDaily"/> off, it does neither, and returns null: no refill message is queued.
    /// </summary>
    // A failed run (every model failing on an empty backlog, Cosmos down) is tried again later in the day; publishing
    // skips a day that already has its rite, so a retry is always safe.
    [Function("DailyRite")]
    [FixedDelayRetry(3, "00:20:00")]
    [QueueOutput(RefillBacklogFunction.QueueName)]
    public async Task<string?> Run([TimerTrigger(Schedule)] TimerInfo timer, CancellationToken cancellationToken)
    {
        if (!options.Value.PublishDaily)
        {
            logger.LogInformation("No rite published today: daily publishing is off (Generation:PublishDaily).");
            return null;
        }

        var result = await publisher.PublishToday(cancellationToken);
        if (result.Exists(published => published.IsNew))
        {
            // Today showed yesterday's rite (or the slumbering page), kept at the edge until purged.
            await pages.Purge([Paths.Today], cancellationToken);
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
