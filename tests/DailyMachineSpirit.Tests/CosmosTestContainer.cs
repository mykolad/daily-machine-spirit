using DailyMachineSpirit.Data;
using DailyMachineSpirit.Data.Repositories;
using Microsoft.Azure.Cosmos;

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
    private Database? database;

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

    public Container Container { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            database = await client.CreateDatabaseAsync($"test-{Guid.NewGuid():N}");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "The Cosmos DB emulator isn't reachable. Start it: docker run -d -p 8081:8081 -p 8080:8080 " +
                "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest --protocol https", ex);
        }
        Container = await database.CreateContainerAsync(ItemRepository.ContainerName, ItemRepository.PartitionKeyPath);
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
            await database.DeleteAsync();
        client.Dispose();
    }
}
