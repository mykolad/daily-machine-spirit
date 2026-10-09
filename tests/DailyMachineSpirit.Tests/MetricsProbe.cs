using System.Diagnostics.Metrics;
using DailyMachineSpirit.Functions.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DailyMachineSpirit.Tests;

/// <summary>A <see cref="SiteMetrics"/> of the test's own, and what it records.</summary>
public sealed class MetricsProbe : IDisposable
{
    private readonly ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();

    public MetricsProbe()
    {
        Metrics = new SiteMetrics(services.GetRequiredService<IMeterFactory>());
    }

    public SiteMetrics Metrics { get; }

    /// <summary>Starts collecting one instrument; only what's recorded from here on is seen.</summary>
    public MetricCollector<T> Collect<T>(string instrument) where T : struct
        => new(services.GetRequiredService<IMeterFactory>(), SiteMetrics.MeterName, instrument);

    /// <summary>The measurements' tags, as "name=value" lists, for comparing in one go.</summary>
    public static IEnumerable<string> Tags<T>(MetricCollector<T> collector, params string[] names) where T : struct
        => collector.GetMeasurementSnapshot().Select(measurement =>
            string.Join(" ", names.Select(name => $"{name}={measurement.Tags[name]}")));

    public void Dispose() => services.Dispose();
}
