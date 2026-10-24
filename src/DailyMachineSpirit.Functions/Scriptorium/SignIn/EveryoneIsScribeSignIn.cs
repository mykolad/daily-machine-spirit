using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// Only for staging, whose IP rule admits its owner alone, and a local run (<see cref="ScriptoriumOptions.WithoutSignIn"/>).
/// Without a sign-in there's no telling Scribes apart: the record names them all as one, on staging.
/// </summary>
public sealed class EveryoneIsScribeSignIn : IScribeSignIn
{
    public Task<Option<ScribeId>> Scribe(HttpRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Some(ScribeId.Staging));
}
