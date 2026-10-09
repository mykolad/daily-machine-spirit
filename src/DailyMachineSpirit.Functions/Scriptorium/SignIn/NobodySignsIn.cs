using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// The Scriptorium is on, but neither Cloudflare Access nor <see cref="ScriptoriumOptions.WithoutSignIn"/> is set up:
/// nobody is let in, rather than everybody.
/// </summary>
public sealed class NobodySignsIn : IScribeSignIn
{
    public Task<bool> IsScribe(HttpRequest request, CancellationToken cancellationToken) => Task.FromResult(false);
}
