using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Azure.Cosmos;

namespace DailyMachineSpirit.Data;

/// <summary>
/// Measures every request the app sends to Cosmos DB: its request units and its duration, by operation and status code.
/// The SDK's own metrics are still in preview. It sits before the SDK's retries, so a throttled request shows up as a
/// slow one, and as a 429 only once the SDK gives up.
/// </summary>
public sealed class CosmosMetricsHandler : RequestHandler
{
    public const string MeterName = "DailyMachineSpirit.Cosmos";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Histogram<double> RequestCharge = Meter.CreateHistogram<double>(
        "dms.cosmos.request_charge", "{request_unit}", "Request units each Cosmos DB request used.",
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [1, 2, 5, 10, 20, 50, 100, 200, 500] });

    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>(
        "dms.cosmos.duration", "s", "How long each Cosmos DB request took, the SDK's retries included.",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30],
        });

    public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await base.SendAsync(request, cancellationToken);
        var tags = new TagList { { "operation", Operation(request) }, { "status_code", (int)response.StatusCode } };
        RequestCharge.Record(response.Headers.RequestCharge, tags);
        Duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        return response;
    }

    // From the method and the SDK's own headers: the request's address names the document, too many values for a tag.
    private static string Operation(RequestMessage request)
        => request.Headers["x-ms-documentdb-isquery"] is not null ? "query"
            : request.Headers["x-ms-cosmos-is-batch-request"] is not null ? "batch"
            : request.Method.Method switch
            {
                "GET" => "read",
                "POST" => "create",
                "PUT" => "replace",
                "PATCH" => "patch",
                "DELETE" => "delete",
                var other => other.ToLowerInvariant(),
            };
}
