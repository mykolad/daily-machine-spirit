using DailyMachineSpirit.Functions.Telemetry;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// Which environment the telemetry says it comes from. Azure names every app's only slot "Production", staging's too; the
/// environment's own setting (infra/) must win. These set process-wide variables, so they run apart from other tests.
/// </summary>
[Collection(nameof(ConfiguredResourceTests))]
[CollectionDefinition(nameof(ConfiguredResourceTests), DisableParallelization = true)]
public sealed class ConfiguredResourceTests : IDisposable
{
    private readonly string? slot = Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME");
    private readonly string? site = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
    private readonly string? attributes = Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES");

    public ConfiguredResourceTests()
    {
        Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", "Production");
        // As on Azure: the worker's detector names the slot only where a site name says it runs there.
        Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "machinespirit-app-staging");
        Environment.SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", "deployment.environment.name=staging");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("WEBSITE_SLOT_NAME", slot);
        Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", site);
        Environment.SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", attributes);
    }

    [Fact]
    public void TheEnvironmentsOwnSetting_NamesIt_OverTheSlot()
        => Assert.Equal("staging", EnvironmentName(telemetry => telemetry.UseFunctionsWorkerDefaults().KeepConfiguredResourceAttributes()));

    // Why it's needed: the Functions worker's defaults alone name the slot.
    [Fact]
    public void TheWorkersDefaultsAlone_NameTheSlot()
        => Assert.Equal("Production", EnvironmentName(telemetry => telemetry.UseFunctionsWorkerDefaults()));

    // Production's standby (#113) reports as the first app, so the dashboards and alerts count both.
    [Fact]
    public void TheEnvironmentsOwnSetting_NamesTheService_OverTheApp()
    {
        Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "machinespirit-app-northeurope");
        Environment.SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES",
            "deployment.environment.name=production,cloud.region=northeurope,service.name=machinespirit-app");

        var resource = TelemetryResource(telemetry => telemetry.UseFunctionsWorkerDefaults().KeepConfiguredResourceAttributes());

        Assert.Equal(("machinespirit-app", "northeurope"), (Attribute(resource, "service.name"), Attribute(resource, "cloud.region")));
    }

    private static object EnvironmentName(Action<IOpenTelemetryBuilder> configure)
        => Attribute(TelemetryResource(configure), "deployment.environment.name");

    private static object Attribute(Resource resource, string key) => resource.Attributes.Single(attribute => attribute.Key == key).Value;

    private static Resource TelemetryResource(Action<IOpenTelemetryBuilder> configure)
    {
        var services = new ServiceCollection();
        var telemetry = services.AddOpenTelemetry();
        configure(telemetry);
        telemetry.WithMetrics(_ => { });
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<MeterProvider>().GetResource();
    }
}
