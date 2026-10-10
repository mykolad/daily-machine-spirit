using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace DailyMachineSpirit.Functions.Telemetry;

/// <summary>
/// Times every invocation for <see cref="SiteMetrics.Invocation"/>. The app's own measure of its requests: the Functions
/// host's, which would carry visitors' user agents, isn't exported (Program.cs).
/// </summary>
public sealed class InvocationMetricsMiddleware : IFunctionsWorkerMiddleware
{
    private readonly SiteMetrics metrics;
    private readonly TimeProvider time;

    public InvocationMetricsMiddleware(SiteMetrics metrics, TimeProvider time)
    {
        this.metrics = metrics;
        this.time = time;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var started = time.GetTimestamp();
        try
        {
            await next(context);
            metrics.Invocation(context.FunctionDefinition.Name, Outcome(context.GetInvocationResult().Value), time.GetElapsedTime(started));
        }
        catch
        {
            metrics.Invocation(context.FunctionDefinition.Name, "failed", time.GetElapsedTime(started));
            throw;
        }
    }

    /// <summary>
    /// An HTTP function's status code, read from the result it returned: the response itself is only written after this
    /// middleware, by the ASP.NET Core integration. Anything else that returns is "ok".
    /// </summary>
    public static string Outcome(object? result) => result switch
    {
        IStatusCodeActionResult { StatusCode: int code } => code.ToString(),
        IActionResult => "200",
        _ => "ok",
    };
}
