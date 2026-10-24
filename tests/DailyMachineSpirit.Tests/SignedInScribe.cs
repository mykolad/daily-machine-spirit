using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>A Scribe signed in with their Access id, as Cloudflare Access would name them, without the token's checks.</summary>
public sealed class SignedInScribe(ScribeId scribe) : IScribeSignIn
{
    public Task<Option<ScribeId>> Scribe(HttpRequest request, CancellationToken cancellationToken) => Task.FromResult(Some(scribe));
}
