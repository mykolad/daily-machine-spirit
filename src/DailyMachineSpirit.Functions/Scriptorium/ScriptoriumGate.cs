using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// Who may see and change the Scriptorium. Turned off, it doesn't exist (404). Only a Scribe may look or act
/// (<see cref="IScribeSignIn"/>), and a form posted from another site (a forged request riding a Scribe's sign-in or
/// address) is forbidden.
/// </summary>
public sealed class ScriptoriumGate
{
    private readonly IScribeSignIn signIn;
    private readonly IOptions<ScriptoriumOptions> options;

    public ScriptoriumGate(IScribeSignIn signIn, IOptions<ScriptoriumOptions> options)
    {
        this.signIn = signIn;
        this.options = options;
    }

    /// <summary>Why a page can't be shown, if it can't.</summary>
    public async Task<Option<IActionResult>> Refusal(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return new NotFoundResult();
        }
        return await signIn.IsScribe(request, cancellationToken)
            ? None
            : Some<IActionResult>(new StatusCodeResult(StatusCodes.Status403Forbidden));
    }

    /// <summary>Which Scribe is looking, for what the page says of them (their name, #84); else why they may not.</summary>
    public async Task<Either<IActionResult, string>> LookingScribe(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return new NotFoundResult();
        }
        return (await signIn.Scribe(request, cancellationToken))
            .ToEither<IActionResult>(() => new StatusCodeResult(StatusCodes.Status403Forbidden));
    }

    /// <summary>Which Scribe acts, for the record (#9); else why they may not.</summary>
    public async Task<Either<IActionResult, string>> ActingScribe(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return new NotFoundResult();
        }
        if (!RequestOrigin.IsFromThisSite(request))
        {
            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
        return (await signIn.Scribe(request, cancellationToken))
            .ToEither<IActionResult>(() => new StatusCodeResult(StatusCodes.Status403Forbidden));
    }
}
