namespace DailyMachineSpirit.Functions.Generation.Chat;

public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>The AI Services account the models are deployed on, e.g. <c>https://&lt;account&gt;.openai.azure.com/</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;
}
