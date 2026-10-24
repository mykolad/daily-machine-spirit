using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// For an app with neither Cloudflare Access nor <see cref="ScriptoriumOptions.WithoutSignIn"/> set up:
/// a forgotten setting locks it rather than opening it to everyone.
/// </summary>
public sealed class NobodyIsScribeSignIn : IScribeSignIn
{
    public Task<Option<ScribeId>> Scribe(HttpRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Option<ScribeId>.None);
}
