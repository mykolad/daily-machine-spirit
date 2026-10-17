using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>How long the writer keeps asking each model: a refill can wait, a Scribe can't.</summary>
/// <param name="AttemptsPerModel">How many times each model is asked before the next one.</param>
/// <param name="RetryDelay">The pause before asking again.</param>
/// <param name="AttemptTime">How long one answer may take, if it's limited.</param>
public sealed record Patience(int AttemptsPerModel, TimeSpan RetryDelay, Option<TimeSpan> AttemptTime);
