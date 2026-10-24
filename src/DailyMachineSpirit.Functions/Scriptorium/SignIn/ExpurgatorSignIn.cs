using DailyMachineSpirit.Data.Entities;
namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// Who may erase words from published rites (<c>/scriptorium/expurgate</c>): a Scribe whom Cloudflare Access admitted
/// to the Expurgators' own application, so the app checks its token against that application's audience
/// (<see cref="CloudflareAccessOptions.ExpurgatorAudience"/>). On staging, without sign-in, the one shared Scribe is an
/// Expurgator too.
/// </summary>
public sealed class ExpurgatorSignIn
{
    public ExpurgatorSignIn(IScribeSignIn signIn)
    {
        SignIn = signIn;
    }

    public IScribeSignIn SignIn { get; }
}
