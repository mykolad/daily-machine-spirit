using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace DailyMachineSpirit.Mcp;

/// <summary>
/// The connector's telemetry, to Grafana Cloud over OTLP as the site's goes (#145), exported only where
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (infra/); a local run and the tests export nothing.
/// </summary>
public static class ConnectorTelemetry
{
    /// <summary>The MCP SDK's spans: one per request, with the tool's name and whether it failed.</summary>
    public const string McpSource = "Experimental.ModelContextProtocol";

    /// <summary>
    /// Traces: the SDK's spans and the reads of the rites API under them. Not ASP.NET Core's request spans, which carry
    /// the caller's user agent. Logs: each tool call (<see cref="LogCounters"/>), and everyone's warnings. No metrics:
    /// with a few calls a day, an instance often serves one call and is gone, and <c>increase()</c> never counts a
    /// series' first sample (observability/README.md), so the dashboard counts the log lines instead.
    /// </summary>
    public static IOpenTelemetryBuilder AddConnectorTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging(logging => logging
            .AddFilter<OpenTelemetryLoggerProvider>(null, LogLevel.Warning)
            .AddFilter<OpenTelemetryLoggerProvider>(typeof(ConnectorTelemetry).Namespace, LogLevel.Information)
            // The host gives the connector its port (Program.cs), over the image's HTTP_PORTS, as it should, which
            // ASP.NET Core warns about at every start; errors still show. On this provider, as its rules beat any other.
            .AddFilter<OpenTelemetryLoggerProvider>("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Error));
        var telemetry = services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(McpSource)
                .AddHttpClientInstrumentation())
            .WithLogging();
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }
        return telemetry;
    }

    /// <summary>
    /// Starts each request's trace at the SDK's span. ASP.NET Core always makes an activity for the request (its
    /// logging asks for one), never recorded here, and the SDK's span, its child, would be dropped with it: the sampler
    /// follows the parent. The <c>traceparent</c> header a caller sends goes with it.
    /// </summary>
    public static WebApplication UseConnectorTelemetry(this WebApplication app)
    {
        app.Use((context, next) =>
        {
            // Only for what follows: the change doesn't flow back to the host, which still stops its own activity.
            Activity.Current = null;
            return next(context);
        });
        return app;
    }
}
