using DailyMachineSpirit.Data;
using Microsoft.Azure.Cosmos;

namespace DailyMachineSpirit.Tests;

/// <summary>Which of the account's regions the app reads from (#113). Creating a client doesn't reach the account.</summary>
public class CosmosClientsTests
{
    private const string Endpoint = "https://machinespirit-cosmos.documents.azure.com:443/";

    // The standby reads from its own replica, not across Europe from the first region.
    [Fact]
    public void AnAppGivenItsRegion_ReadsThere()
    {
        using var client = CosmosClients.Create(new CosmosOptions { Endpoint = Endpoint, Region = Regions.NorthEurope });

        Assert.Equal(Regions.NorthEurope, client.ClientOptions.ApplicationRegion);
    }

    [Fact]
    public void AnAppWithoutARegion_LeavesItToTheAccount()
    {
        using var client = CosmosClients.Create(new CosmosOptions { Endpoint = Endpoint });

        Assert.Null(client.ClientOptions.ApplicationRegion);
    }
}
