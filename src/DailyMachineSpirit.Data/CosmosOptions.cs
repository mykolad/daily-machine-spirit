using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace DailyMachineSpirit.Data;

public class CosmosOptions
{
    public const string SectionName = "Cosmos";

    /// <summary>The account's endpoint, e.g. <c>https://&lt;account&gt;.documents.azure.com:443/</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// The region the app runs in, as Cosmos DB names it ("North Europe"), so it reads from the replica there (#113)
    /// and, should that fail, from the nearest other; empty reads from the account's first region (a local run).
    /// </summary>
    public string Region { get; set; } = string.Empty;
}

public static class CosmosClients
{
    /// <summary>
    /// Signs in with Entra ID (the app's managed identity in Azure, your <c>az login</c> locally): the account's keys
    /// stay off.
    /// </summary>
    public static CosmosClient Create(CosmosOptions options)
    {
        var clientOptions = ClientOptions();
        if (options.Region.Length > 0)
        {
            clientOptions.ApplicationRegion = options.Region;
        }
        return new(options.Endpoint, new DefaultAzureCredential(), clientOptions);
    }

    internal static CosmosClientOptions ClientOptions() => new()
    {
        ApplicationName = "DailyMachineSpirit",
        // A span per operation (the "Azure.Cosmos.Operation" source) for the app's telemetry; the SDK leaves it off.
        CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions { DisableDistributedTracing = false },
        CustomHandlers = { new CosmosMetricsHandler() },
        // System.Text.Json, like the rest of the app, rather than the SDK's default Newtonsoft.Json.
        UseSystemTextJsonSerializerWithOptions = DocumentJson(),
    };

    /// <summary>How the documents are written as JSON: in Cosmos DB, and in the rites' snapshot (<c>RiteSnapshotDocument</c>).</summary>
    internal static JsonSerializerOptions DocumentJson() => new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        // A stored document can still say "title": null. By default that would put a null into a non-nullable string;
        // this makes reading it fail right here instead.
        RespectNullableAnnotations = true,
    };
}
