using DailyMachineSpirit.Functions.Generation.Precepts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions;

/// <summary>
/// Distils the Precepts (#12) every week, from the week's judgments with the earlier ones, unless no rite was judged since
/// the last distillation; a Scribe can ask sooner.
/// </summary>
public sealed class DistilPreceptsFunction
{
    // Mondays at 01:00 UTC: after a weekend's judgments, and clear of midnight's publishing.
    public const string Schedule = "0 0 1 * * 1";

    private readonly PreceptDistiller distiller;
    private readonly ILogger<DistilPreceptsFunction> logger;

    public DistilPreceptsFunction(PreceptDistiller distiller, ILogger<DistilPreceptsFunction> logger)
    {
        this.distiller = distiller;
        this.logger = logger;
    }

    // Nothing (new) to learn from, or every precept pinned, is a quiet week. A failure is logged here, since only the app's
    // telemetry is exported, then thrown, so the host counts the run as failed and tries again.
    [Function("DistilPrecepts")]
    [FixedDelayRetry(2, "00:30:00")]
    public async Task Run([TimerTrigger(Schedule)] TimerInfo timer, CancellationToken cancellationToken)
    {
        var result = await distiller.DistilIfJudgedSinceLast(cancellationToken);
        result.Match(
            Right: book => logger.LogInformation("The Precepts were distilled: {Count} in all.", book.Precepts.Count),
            Left: error =>
            {
                if (error == PreceptDistiller.NothingToLearn || error == PreceptDistiller.NothingNewToLearn || error == PreceptDistiller.AllPinned)
                {
                    logger.LogInformation("The Precepts weren't distilled: {Reason}", error.Message);
                    return;
                }
                logger.LogError(error.ToException(), "The Precepts couldn't be distilled: {Reason}", error.Message);
                throw error.ToException();
            });
    }
}
