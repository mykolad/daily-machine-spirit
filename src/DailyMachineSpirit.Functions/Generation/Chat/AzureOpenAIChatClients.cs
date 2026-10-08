using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace DailyMachineSpirit.Functions.Generation.Chat;

/// <summary>
/// Signs in with Entra ID (the app's managed identity in Azure, your <c>az login</c> locally): the account's keys stay
/// off, as with Cosmos DB.
/// </summary>
public sealed class AzureOpenAIChatClients : IChatClients
{
    private readonly AzureOpenAIClient client;

    public AzureOpenAIChatClients(Uri endpoint)
    {
        client = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
    }

    public IChatClient For(string model) => client.GetChatClient(model).AsIChatClient();
}
