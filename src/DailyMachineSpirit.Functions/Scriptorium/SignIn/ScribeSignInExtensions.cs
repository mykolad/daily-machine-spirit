using Microsoft.AspNetCore.Http;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

public static class ScribeSignInExtensions
{
    /// <summary>Whether the request comes from a Scribe, whichever one.</summary>
    public static async Task<bool> IsScribe(this IScribeSignIn signIn, HttpRequest request, CancellationToken cancellationToken)
        => (await signIn.Scribe(request, cancellationToken)).IsSome;
}
