using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// Gives the drafts burned before a draft kept its burn's details (who, from where, why) the ones the Record and the
/// Scribes' decisions hold, each time the app starts, so the Ashes can say them. After the first run there's nothing
/// left, and it's one small query. A burn older than the Record gets nothing to say, and isn't looked for again. A
/// failure is logged, and the next start tries again.
/// </summary>
public sealed class BurnBackfill : BackgroundService
{
    private readonly IBurnRepository burns;
    private readonly ILogger<BurnBackfill> logger;

    public BurnBackfill(IBurnRepository burns, ILogger<BurnBackfill> logger)
    {
        this.burns = burns;
        this.logger = logger;
    }

    /// <summary>How many burned drafts it gave their details.</summary>
    public async Task<Either<Error, int>> Run(CancellationToken cancellationToken)
        => await (await burns.GetBurnedWithoutDetails(cancellationToken)).BindAsync(async drafts =>
        {
            // Stops at the first failure; the next start picks up what's left.
            var given = 0;
            foreach (var draftId in drafts)
            {
                var saved = await (await burns.FindInTheRecord(draftId, cancellationToken))
                    .BindAsync(burn => burns.SaveDetails(draftId, burn, cancellationToken));
                if (saved.IsLeft)
                {
                    return saved.Map(_ => given);
                }
                given++;
            }
            return Prelude.Right<Error, int>(given);
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            (await Run(stoppingToken)).Match(
                Right: given =>
                {
                    if (given > 0)
                    {
                        logger.LogInformation("Gave {Count} burned drafts who burned them, from where and why, from the Record.", given);
                    }
                },
                Left: error => logger.LogWarning(error.ToException(), "Older burns couldn't be given their details; the next start tries again: {Reason}", error.Message));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is stopping: the next start tries again.
        }
    }
}
