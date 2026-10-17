using DailyMachineSpirit.Functions.Generation.Chat;
using Microsoft.Extensions.AI;

namespace DailyMachineSpirit.Tests;

public class AzureOpenAIChatClientsTests
{
    [Fact]
    public void For_GivesAClientForThatDeployment()
    {
        var clients = new AzureOpenAIChatClients("https://machinespirit-ai.openai.azure.com/");

        var client = clients.For("gpt-6-luna");

        Assert.Equal("gpt-6-luna", client.GetService<ChatClientMetadata>()?.DefaultModelId);
    }

    // The Scriptorium takes the writer for rewrites: without the models' settings it must still start, and only asking a
    // model fails (which the writer counts as that model's failure).
    [Fact]
    public void WithoutAnEndpoint_OnlyAskingAModelFails()
    {
        var clients = new AzureOpenAIChatClients("");

        Assert.Throws<UriFormatException>(() => clients.For("gpt-6-sol"));
    }
}
