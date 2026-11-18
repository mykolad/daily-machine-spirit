using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Pages;
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
    private readonly ILogger<DailyRiteFunction> logger;

    public DailyRiteFunction(DailyRitePublisher publisher, IPageCache pages, ILogger<DailyRiteFunction> logger)
    {
        this.publisher = publisher;
        this.pages = pages;
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
        if (result.Case is PublishedRite { IsNew: true } published)
        {
            // Today showed yesterday's rite (or the slumbering page), and the search field's index, the rites API, the
            // sitemap and the feed lacked the new one: all are kept at the edge until purged. So are the pages of the two
            // rites before it (#195): yesterday's now has a next rite, which leads to Today, and the day before's next no
            // longer leads to Today but to yesterday's page. Rites are numbered one after another, and never removed, so
            // they're the two numbers before; were one ever missing, its page would catch up within its hour.
            List<string> stale = [Paths.Today, Paths.SearchIndex, Paths.RitesApi, Paths.Sitemap, Paths.Feed];
            stale.AddRange(Enumerable.Range(published.Rite.Number - 2, 2).Where(number => number >= 1).Select(Paths.Rite));
            await pages.Purge(stale, cancellationToken);
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
