using DailyMachineSpirit.Functions.Telemetry;
using Microsoft.AspNetCore.Mvc;

namespace DailyMachineSpirit.Tests;

public class InvocationMetricsMiddlewareTests
{
    public static TheoryData<object?, string> Results => new()
    {
        { new ContentResult { StatusCode = 404, Content = "Not found" }, "404" },
        { new StatusCodeResult(503), "503" },
        { new OkObjectResult(new { blessed = 1 }), "200" },
        // A result that names no status code (a file, a page without one) is written as 200.
        { new ContentResult { Content = "Today" }, "200" },
        { new FileContentResult([1, 2, 3], "font/woff2"), "200" },
        // The timer returns nothing.
        { null, "ok" },
    };

    [Theory]
    [MemberData(nameof(Results))]
    public void Outcome_IsTheStatusCodeAnHttpFunctionReturned(object? result, string outcome)
        => Assert.Equal(outcome, InvocationMetricsMiddleware.Outcome(result));
}
