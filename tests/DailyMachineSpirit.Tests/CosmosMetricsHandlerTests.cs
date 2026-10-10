using DailyMachineSpirit.Data;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DailyMachineSpirit.Tests;

public class CosmosMetricsHandlerTests
{
    public static TheoryData<Exception, string> NoResponse => new()
    {
        { new HttpRequestException("No route to the account."), "failed" },
        { new OperationCanceledException(), "canceled" },
    };

    [Theory]
    [MemberData(nameof(NoResponse))]
    public async Task ARequestThatGetsNoResponse_IsStillMeasured(Exception exception, string statusCode)
    {
        // The meter is shared with tests running alongside, so this looks for its own request among theirs.
        using var durations = new MetricCollector<double>(null, CosmosMetricsHandler.MeterName, "dms.cosmos.duration");
        var handler = new CosmosMetricsHandler { InnerHandler = new Throwing(exception) };
        var request = new RequestMessage(HttpMethod.Patch, new Uri("dbs/machinespirit/colls/rites/docs/2026-10-07", UriKind.Relative));

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => handler.SendAsync(request, CancellationToken.None));

        Assert.Same(exception, thrown);
        Assert.Contains(durations.GetMeasurementSnapshot(),
            measurement => (string?)measurement.Tags["operation"] == "patch" && (string?)measurement.Tags["status_code"] == statusCode);
    }

    private sealed class Throwing(Exception exception) : RequestHandler
    {
        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<ResponseMessage>(exception);
    }
}
