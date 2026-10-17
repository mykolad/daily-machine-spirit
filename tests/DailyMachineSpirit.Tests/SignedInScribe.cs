using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>A Scribe signed in with their Access id, as Cloudflare Access would name them, without the token's checks.</summary>
public sealed class SignedInScribe(string accessId) : IScribeSignIn
{
    public Task<Option<string>> Scribe(HttpRequest request, CancellationToken cancellationToken) => Task.FromResult(Some(accessId));
}
