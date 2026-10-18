using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// Who may see and change the Scriptorium. Only a Scribe may look or act (<see cref="IScribeSignIn"/>), and a form
/// posted from another site (a forged request riding a Scribe's sign-in or address) is forbidden.
/// </summary>
public sealed class ScriptoriumGate
{
    private readonly IScribeSignIn signIn;

    public ScriptoriumGate(IScribeSignIn signIn)
    {
        this.signIn = signIn;
    }

    /// <summary>Why a page can't be shown, if it can't.</summary>
    public async Task<Option<IActionResult>> Refusal(HttpRequest request, CancellationToken cancellationToken)
        => await signIn.IsScribe(request, cancellationToken)
            ? None
            : Some<IActionResult>(new StatusCodeResult(StatusCodes.Status403Forbidden));

    /// <summary>Which Scribe is looking, for what the page says of them (their name, #84); else why they may not.</summary>
    public async Task<Either<IActionResult, string>> LookingScribe(HttpRequest request, CancellationToken cancellationToken)
        => (await signIn.Scribe(request, cancellationToken))
            .ToEither<IActionResult>(() => new StatusCodeResult(StatusCodes.Status403Forbidden));

    /// <summary>Which Scribe acts, for the record (#9); else why they may not.</summary>
    public async Task<Either<IActionResult, string>> ActingScribe(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!RequestOrigin.IsFromThisSite(request))
        {
            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
        return (await signIn.Scribe(request, cancellationToken))
            .ToEither<IActionResult>(() => new StatusCodeResult(StatusCodes.Status403Forbidden));
    }
}
