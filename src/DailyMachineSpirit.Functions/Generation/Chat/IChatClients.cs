using Microsoft.Extensions.AI;

namespace DailyMachineSpirit.Functions.Generation.Chat;

public interface IChatClients
{
    /// <summary>A client for one model deployment, by its name (<see cref="GenerationOptions.Models"/>).</summary>
    IChatClient For(string model);
}
