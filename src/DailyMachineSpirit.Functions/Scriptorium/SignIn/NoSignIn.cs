using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// Everyone who reaches the Scriptorium is a Scribe: only for staging, whose IP rule admits its owner alone
/// (<see cref="ScriptoriumOptions.WithoutSignIn"/>).
/// </summary>
public sealed class NoSignIn : IScribeSignIn
{
    public Task<bool> IsScribe(HttpRequest request, CancellationToken cancellationToken) => Task.FromResult(true);
}
