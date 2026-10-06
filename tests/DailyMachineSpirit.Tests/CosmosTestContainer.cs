using System.Net;
using DailyMachineSpirit.Data;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using Microsoft.Azure.Cosmos;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// A fresh database and container in the Cosmos DB emulator for every test (xUnit makes a new test class instance per
/// test, and each holds one of these), deleted again afterwards, so tests never see each other's data. The tests need
/// the emulator running (CI starts it; locally see CLAUDE.md), at <see cref="EndpointVariable"/> or localhost:8081.
/// </summary>
public sealed class CosmosTestContainer : IAsyncLifetime
{
    public const string EndpointVariable = "COSMOS_TEST_ENDPOINT";
    // The emulator's well-known key, published in its documentation: it protects nothing but the local emulator.
    private const string EmulatorKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private readonly CosmosClient client;
    private Option<Database> database = None;

    public CosmosTestContainer()
    {
        var options = CosmosClients.ClientOptions();
        // The emulator speaks HTTPS with a self-signed certificate, and only in gateway mode.
        options.ConnectionMode = ConnectionMode.Gateway;
        options.LimitToEndpoint = true;
        options.HttpClientFactory = () => new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        });
        var endpoint = Environment.GetEnvironmentVariable(EndpointVariable) is { Length: > 0 } configured ? configured : "https://localhost:8081/";
        client = new CosmosClient(endpoint, EmulatorKey, options);
    }

    public Container Container => database
        .Map(created => created.GetContainer(RiteRepository.ContainerName))
        .IfNone(() => throw new InvalidOperationException("The test database is created in InitializeAsync."));

    public async Task InitializeAsync()
    {
        Database created;
        try
        {
            created = await client.CreateDatabaseAsync($"test-{Guid.NewGuid():N}");
        }
        // Nothing listening: HttpRequestException. Still starting up: the SDK's 503.
        catch (Exception ex) when (ex is HttpRequestException
            || ex is CosmosException { StatusCode: HttpStatusCode.ServiceUnavailable })
        {
            throw new InvalidOperationException(
                "The Cosmos DB emulator isn't reachable or is still starting. Start it, and wait for http://localhost:8080/ready: " +
                "docker run -d -p 8081:8081 -p 8080:8080 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest --protocol https", ex);
        }
        database = created;
        await created.CreateContainerAsync(RiteRepository.ContainerName, RiteRepository.PartitionKeyPath);
    }

    public async Task DisposeAsync()
    {
        await database.IfSomeAsync(created => created.DeleteAsync());
        client.Dispose();
    }
}
