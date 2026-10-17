using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace DailyMachineSpirit.Functions.Generation.Chat;

/// <summary>
/// Signs in with Entra ID (the app's managed identity in Azure, your <c>az login</c>): the account's keys stay off, as
/// with Cosmos DB. The client is made when a model is first asked for, not with this: the Scriptorium's functions take
/// the writer (for rewrites), and they must still serve every other request where <c>AzureOpenAI:Endpoint</c> isn't set.
/// </summary>
public sealed class AzureOpenAIChatClients : IChatClients
{
    private readonly Lazy<AzureOpenAIClient> client;

    public AzureOpenAIChatClients(string endpoint)
    {
        client = new Lazy<AzureOpenAIClient>(() => new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential()));
    }

    public IChatClient For(string model) => client.Value.GetChatClient(model).AsIChatClient();
}
