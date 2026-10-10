using DailyMachineSpirit.Functions.Generation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions;

/// <summary>
/// Refills the backlog whenever a message asks for it. A queue, rather than doing it in the caller, because writing a
/// full backlog takes minutes: the daily timer and the Scriptorium only leave a message.
/// </summary>
public sealed class RefillBacklogFunction
{
    public const string QueueName = "backlog-refills";
    public const string AfterPublishing = "after publishing";

    private readonly BacklogRefiller refiller;
    private readonly ILogger<RefillBacklogFunction> logger;

    public RefillBacklogFunction(BacklogRefiller refiller, ILogger<RefillBacklogFunction> logger)
    {
        this.refiller = refiller;
        this.logger = logger;
    }

    // A failure is thrown, so the message comes back for another try (host.json: up to 3, 10 minutes apart). The drafts
    // written before it stay.
    [Function("RefillBacklog")]
    public async Task Run([QueueTrigger(QueueName)] string reason, CancellationToken cancellationToken)
    {
        var result = await refiller.Refill(cancellationToken);
        result.Match(
            Right: written => logger.LogInformation("Refilled the backlog ({Reason}): {Written} new drafts.", reason, written),
            Left: error => throw error.ToException());
    }
}
