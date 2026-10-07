using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace DailyMachineSpirit.Functions.Generation;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>The AI Services account the models are deployed on, e.g. <c>https://&lt;account&gt;.openai.azure.com/</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;
}

public interface IChatClients
{
    /// <summary>A client for one model deployment, by its name (<see cref="GenerationOptions.Models"/>).</summary>
    IChatClient For(string model);
}

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
