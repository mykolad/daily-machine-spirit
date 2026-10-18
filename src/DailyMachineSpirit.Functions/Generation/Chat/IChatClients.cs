using Microsoft.Extensions.AI;

namespace DailyMachineSpirit.Functions.Generation.Chat;

public interface IChatClients
{
    /// <summary>A client for one model deployment, by its name (as <c>machine-spirit.jsonc</c> names it).</summary>
    IChatClient For(string model);
}
