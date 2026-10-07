using DailyMachineSpirit.Functions.Generation;
using Microsoft.Extensions.AI;

namespace DailyMachineSpirit.Tests;

public class AzureOpenAIChatClientsTests
{
    [Fact]
    public void For_GivesAClientForThatDeployment()
    {
        var clients = new AzureOpenAIChatClients(new Uri("https://machinespirit-ai.openai.azure.com/"));

        var client = clients.For("gpt-6-luna");

        Assert.Equal("gpt-6-luna", client.GetService<ChatClientMetadata>()?.DefaultModelId);
    }
}
