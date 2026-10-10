using System.Diagnostics.Metrics;
using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Telemetry;

/// <summary>
/// The site's own metrics, next to the ones .NET, HttpClient and the Cosmos DB SDK already have (Program.cs). Their tags
/// name functions, models and outcomes, never anything about a visitor.
/// </summary>
public sealed class SiteMetrics
{
    public const string MeterName = "DailyMachineSpirit";

    private readonly Histogram<double> invocations;
    private readonly Counter<long> answers;
    private readonly Counter<long> published;
    private readonly Counter<long> scoring;
    private readonly Counter<long> refusals;

    public SiteMetrics(IMeterFactory meters)
    {
        var meter = meters.Create(MeterName);
        invocations = meter.CreateHistogram<double>("dms.invocation.duration", "s",
            "How long each function invocation took, by function and outcome (an HTTP status code, or ok/failed).",
            advice: new InstrumentAdvice<double>
            {
                // From a font served from memory to a model writing a rite.
                HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 120],
            });
        answers = meter.CreateCounter<long>("dms.generation.answers", "{answer}",
            "The models' answers when asked for a rite: accepted, refused (it broke a rule) or failed (no usable answer).");
        published = meter.CreateCounter<long>("dms.rites.published", "{rite}", "Rites published, by kind and model.");
        scoring = meter.CreateCounter<long>("dms.scoring.runs", "{run}",
            "Scoring a new draft (for the Augury and More rites): scored, failed or off.");
        refusals = meter.CreateCounter<long>("dms.scriptorium.refusals", "{request}",
            "Scriptorium requests whose Access token was refused, by reason: the token check that failed, or keys-unavailable.");
    }

    public void Invocation(string function, string outcome, TimeSpan duration)
        => invocations.Record(duration.TotalSeconds, new("function", function), new("outcome", outcome));

    public void Answer(string model, string outcome) => answers.Add(1, new("model", model), new("outcome", outcome));

    public void Published(Rite rite)
        => published.Add(1, new("kind", rite.Kind.ToString().ToLowerInvariant()), new("model", rite.GeneratedByModel));

    public void Scoring(string outcome) => scoring.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void ScribeRefused(string reason) => refusals.Add(1, new KeyValuePair<string, object?>("reason", reason));
}
