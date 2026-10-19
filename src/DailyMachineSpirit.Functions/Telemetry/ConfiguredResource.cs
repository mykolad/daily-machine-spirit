using OpenTelemetry;
using OpenTelemetry.Resources;

namespace DailyMachineSpirit.Functions.Telemetry;

/// <summary>
/// The telemetry's own description of the app, as <c>OTEL_RESOURCE_ATTRIBUTES</c> sets it (infra/), over the Functions
/// worker's defaults. Those set <c>deployment.environment.name</c> from <c>WEBSITE_SLOT_NAME</c>, which is "Production"
/// for every app's only slot, staging's too, and their detector runs after the one that reads our setting.
/// </summary>
public static class ConfiguredResource
{
    /// <summary>Call after <c>UseFunctionsWorkerDefaults</c>: the later detector wins.</summary>
    public static IOpenTelemetryBuilder KeepConfiguredResourceAttributes(this IOpenTelemetryBuilder telemetry)
        => telemetry.ConfigureResource(resource => resource.AddEnvironmentVariableDetector());
}
