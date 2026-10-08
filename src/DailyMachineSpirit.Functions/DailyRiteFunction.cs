using DailyMachineSpirit.Functions.Generation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions;

public sealed class DailyRiteFunction
{
    // Midnight UTC, when a rite's day begins. The host remembers the last run (schedule monitoring), so a run missed while
    // the app was down happens as soon as it's back.
    public const string Schedule = "0 0 0 * * *";

    private readonly DailyRitePublisher publisher;
    private readonly ILogger<DailyRiteFunction> logger;

    public DailyRiteFunction(DailyRitePublisher publisher, ILogger<DailyRiteFunction> logger)
    {
        this.publisher = publisher;
        this.logger = logger;
    }

    // A failed run (every model failing, Cosmos down) is tried again later in the day; publishing skips a day that
    // already has its rite, so a retry is always safe.
    [Function("DailyRite")]
    [FixedDelayRetry(3, "00:20:00")]
    public async Task Run([TimerTrigger(Schedule)] TimerInfo timer, CancellationToken cancellationToken)
    {
        var result = await publisher.PublishToday(cancellationToken);
        result.Match(
            Right: published => logger.LogInformation("Rite NO. {Number} for {Day}: {Outcome}.",
                published.Rite.Number, published.Rite.PublishedOnUtc, published.IsNew ? "published now" : "already published"),
            // Logged here, since only the app's telemetry is exported, then thrown, so the host counts the run as failed
            // (and retries it).
            Left: error =>
            {
                logger.LogError(error.ToException(), "Today's rite couldn't be published: {Reason}", error.Message);
                throw error.ToException();
            });
    }
}
