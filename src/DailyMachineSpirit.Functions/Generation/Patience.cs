using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Generation;

/// <summary>
/// How long the Machine Spirit keeps asking a model (<c>machine-spirit.json</c>): how many tries each model gets, the
/// pause between them, and how long a try, or all of them together, may take. A time left out is no limit of our own.
/// </summary>
public sealed record Patience
{
    public int TriesPerModel { get; init; } = 1;

    public TimeSpan PauseBetweenTries { get; init; }

    public TimeSpan? TimePerTry { get; init; }

    public TimeSpan? TimeInAll { get; init; }

    public Option<TimeSpan> TryLimit => Optional(TimePerTry);

    public Option<TimeSpan> OverallLimit => Optional(TimeInAll);

    /// <summary>Cancelled when one try has had its time; never, without a limit.</summary>
    public CancellationTokenSource TimerForATry(TimeProvider time) => Timer(TryLimit, time);

    /// <summary>Cancelled when all the tries together have had their time; never, without a limit.</summary>
    public CancellationTokenSource TimerForAllTries(TimeProvider time) => Timer(OverallLimit, time);

    private static CancellationTokenSource Timer(Option<TimeSpan> limit, TimeProvider time)
        => limit.Match(Some: after => new CancellationTokenSource(after, time), None: () => new CancellationTokenSource());
}
