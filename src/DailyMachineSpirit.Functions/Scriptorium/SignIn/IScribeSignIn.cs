using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// Whether a request to the Scriptorium comes from a Scribe. Production signs the Scribes in with Cloudflare Access
/// (<see cref="CloudflareAccessSignIn"/>); staging relies on its IP rule (<see cref="NoSignIn"/>); an app with neither
/// set up admits nobody (<see cref="NobodySignsIn"/>).
/// </summary>
public interface IScribeSignIn
{
    Task<bool> IsScribe(HttpRequest request, CancellationToken cancellationToken);
}
