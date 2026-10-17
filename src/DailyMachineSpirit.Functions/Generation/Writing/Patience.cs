using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>How long the writer keeps asking each model: a refill can wait, a Scribe can't.</summary>
public sealed record Patience(int AttemptsPerModel, TimeSpan RetryDelay, Option<TimeSpan> AttemptTime);
