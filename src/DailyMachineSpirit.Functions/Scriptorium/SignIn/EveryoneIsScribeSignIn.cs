using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// Only for staging, whose IP rule admits its owner alone, and a local run (<see cref="ScriptoriumOptions.WithoutSignIn"/>).
/// </summary>
public sealed class EveryoneIsScribeSignIn : IScribeSignIn
{
    public Task<bool> IsScribe(HttpRequest request, CancellationToken cancellationToken) => Task.FromResult(true);
}
