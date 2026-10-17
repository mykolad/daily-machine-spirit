using LanguageExt;
using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// Which Scribe a request to the Scriptorium comes from, if any. Production signs the Scribes in with Cloudflare Access
/// (<see cref="CloudflareAccessSignIn"/>); staging relies on its IP rule (<see cref="EveryoneIsScribeSignIn"/>); an app
/// with neither set up admits nobody (<see cref="NobodyIsScribeSignIn"/>).
/// </summary>
public interface IScribeSignIn
{
    /// <summary>The Scribe's id, as the record (#9) keeps it: never a name or an email. None for anyone else.</summary>
    Task<Option<string>> Scribe(HttpRequest request, CancellationToken cancellationToken);
}
