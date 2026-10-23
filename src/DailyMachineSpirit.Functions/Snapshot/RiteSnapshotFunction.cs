using Microsoft.Azure.Functions.Worker;

namespace DailyMachineSpirit.Functions.Snapshot;

/// <summary>
/// Writes the rites' snapshot every six hours, catching up what changed between rites: votes, tags, ailments. Also the
/// first snapshot of a new environment, if no rite comes sooner.
/// </summary>
public sealed class RiteSnapshotFunction
{
    public const string Schedule = "0 15 */6 * * *";

    private readonly RiteSnapshots snapshots;

    public RiteSnapshotFunction(RiteSnapshots snapshots)
    {
        this.snapshots = snapshots;
    }

    [Function("WriteRiteSnapshot")]
    public async Task Run([TimerTrigger(Schedule)] TimerInfo timer, CancellationToken cancellationToken)
        => await snapshots.Write(cancellationToken);
}
