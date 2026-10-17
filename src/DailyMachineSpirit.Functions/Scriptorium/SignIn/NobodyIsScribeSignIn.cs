using LanguageExt;
using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// For a Scriptorium that's on with neither Cloudflare Access nor <see cref="ScriptoriumOptions.WithoutSignIn"/> set up:
/// a forgotten setting locks it rather than opening it to everyone.
/// </summary>
public sealed class NobodyIsScribeSignIn : IScribeSignIn
{
    public Task<Option<string>> Scribe(HttpRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Option<string>.None);
}
