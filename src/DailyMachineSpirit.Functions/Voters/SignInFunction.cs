using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Voters;

/// <summary>
/// Readers sign in to vote, with any <see cref="ISignInProvider"/> (GitHub for now): <c>/auth/github</c> sends them
/// there, the provider sends them back to <c>/auth/github/callback</c>, and the <see cref="VoterSession"/> cookie keeps
/// them signed in. Separate from the Scriptorium, whose Scribes are whoever Cloudflare Access let in: signing in here
/// makes nobody a Scribe. A provider whose settings, or the voters' secrets, aren't set has no sign-in pages.
/// </summary>
public sealed class SignInFunction
{
    public const string StateCookieName = "dms-signin-state";

    // Long enough to sign in at the provider, short enough that an old state is useless.
    private static readonly TimeSpan StateLasts = TimeSpan.FromMinutes(10);
    private const int MaxReturnPathLength = 512;

    private readonly IEnumerable<ISignInProvider> providers;
    private readonly VoterSession session;
    private readonly IOptions<VoterOptions> voters;
    private readonly SiteMetrics metrics;
    private readonly ILogger<SignInFunction> logger;

    public SignInFunction(
        IEnumerable<ISignInProvider> providers,
        VoterSession session,
        IOptions<VoterOptions> voters,
        SiteMetrics metrics,
        ILogger<SignInFunction> logger)
    {
        this.providers = providers;
        this.session = session;
        this.voters = voters;
        this.metrics = metrics;
        this.logger = logger;
    }

    /// <summary>Off to the provider, remembering where to come back to (<c>?return=/r/12</c>).</summary>
    [Function("SignIn")]
    public IActionResult SignIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/{provider}")] HttpRequest request,
        string provider)
    {
        NeverCached(request);
        return Available(provider).Match<IActionResult>(
            Some: signIn =>
            {
                // The state ties the provider's answer to this browser and this provider: a callback whose state isn't
                // the one in its cookie was started somewhere else, perhaps to sign the reader in as someone else.
                var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
                var returnPath = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(SafeReturnPath(request.Query["return"].ToString())));
                request.HttpContext.Response.Cookies.Append(StateCookieName, $"{state}.{signIn.Name}.{returnPath}", Cookie(StateLasts));
                return Redirect(signIn.AuthorizeUrl(state));
            },
            None: () => new NotFoundResult());
    }

    [Function("SignInCallback")]
    public async Task<IActionResult> Callback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/{provider}/callback")] HttpRequest request,
        string provider,
        CancellationToken cancellationToken)
    {
        NeverCached(request);
        return await Available(provider).MatchAsync(
            Some: signIn => Callback(request, signIn, cancellationToken),
            None: () => new NotFoundResult());
    }

    /// <summary>A form posted from this site: another site's link, image or form can't sign a reader out.</summary>
    [Function("SignOut")]
    public IActionResult SignOut(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signout")] HttpRequest request)
    {
        NeverCached(request);
        if (!RequestOrigin.IsFromThisSite(request))
        {
            return new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
        request.HttpContext.Response.Cookies.Delete(VoterSession.CookieName, Cookie(TimeSpan.Zero));
        return Redirect(SafeReturnPath(request.Query["return"].ToString()));
    }

    /// <summary>
    /// Whether readers can sign in here at all, and whether this one is, for the page's script: the pages themselves are
    /// the same for everyone.
    /// </summary>
    [Function("Me")]
    public IActionResult Me(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/me")] HttpRequest request)
    {
        NeverCached(request);
        var signInAvailable = voters.Value.IsConfigured && providers.Any(signIn => signIn.IsConfigured);
        return new OkObjectResult(new { signInAvailable, signedIn = session.SignedIn(request).IsSome });
    }

    private async Task<IActionResult> Callback(HttpRequest request, ISignInProvider signIn, CancellationToken cancellationToken)
    {
        var started = StartedHere(request);
        request.HttpContext.Response.Cookies.Delete(StateCookieName, Cookie(TimeSpan.Zero));
        var returnPath = started.Map(state => state.ReturnPath).IfNone("/");
        var stateMatches = started.Exists(state =>
            state.Provider == signIn.Name && FixedTimeEquals(state.State, request.Query["state"].ToString()));
        if (!stateMatches)
        {
            metrics.SignIn(signIn.Name, "refused");
            return new BadRequestResult();
        }

        // Back where they were, signed out, when the reader said no at the provider ("access_denied"). Any other error,
        // such as redirect_uri_mismatch or a suspended app, is the site's own setup failing, so it's counted and logged.
        var error = request.Query["error"].ToString();
        if (error == "access_denied")
        {
            metrics.SignIn(signIn.Name, "declined");
            return Redirect(returnPath);
        }
        var code = request.Query["code"].ToString();
        if (error.Length > 0 || code.Length == 0)
        {
            logger.LogWarning("{Provider} sent a reader back without a sign-in: {Error}", signIn.Name, error.Length > 0 ? error : "no code");
            metrics.SignIn(signIn.Name, "failed");
            return Redirect(returnPath);
        }

        return (await signIn.AccountFor(code, cancellationToken)).Match(
            Right: accountId =>
            {
                request.HttpContext.Response.Cookies.Append(VoterSession.CookieName,
                    session.Issue(session.VoterKeyFor(signIn.Name, accountId)), Cookie(VoterSession.Lasts));
                metrics.SignIn(signIn.Name, "signed-in");
                return Redirect(returnPath);
            },
            Left: failure =>
            {
                logger.LogWarning(failure.ToException(), "A reader couldn't sign in with {Provider}: {Reason}", signIn.Name, failure.Message);
                metrics.SignIn(signIn.Name, "failed");
                return Redirect(returnPath);
            });
    }

    // The provider of that name, if it and the voters' secrets are set up; an unknown name is no sign-in page at all.
    private Option<ISignInProvider> Available(string name)
        => voters.Value.IsConfigured
            ? providers.Find(signIn => signIn.Name == name && signIn.IsConfigured)
            : None;

    private static Option<(string State, string Provider, string ReturnPath)> StartedHere(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(StateCookieName, out var cookie) || cookie is null)
        {
            return None;
        }
        var parts = cookie.Split('.');
        if (parts.Length != 3 || parts[0].Length == 0)
        {
            return None;
        }
        try
        {
            return Some((parts[0], parts[1], SafeReturnPath(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[2])))));
        }
        catch (FormatException)
        {
            return None;
        }
    }

    // Only a path on this site: "//evil.example" or "/\evil.example" would send the reader to another one.
    private static string SafeReturnPath(string path)
        => path.Length is > 0 and <= MaxReturnPathLength
           && path[0] == '/'
           && !path.StartsWith("//", StringComparison.Ordinal)
           && !path.StartsWith("/\\", StringComparison.Ordinal)
           && !path.Any(char.IsControl)
            ? path
            : "/";

    private static CookieOptions Cookie(TimeSpan lasts) => new()
    {
        HttpOnly = true,
        Secure = true,
        // Lax: sent when the provider sends the reader back (a top-level GET), never with another site's posts.
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = lasts,
    };

    private static void NeverCached(HttpRequest request) => request.HttpContext.Response.Headers.CacheControl = "no-store";

    private static IActionResult Redirect(string url) => new RedirectResult(url) { PreserveMethod = false, Permanent = false };

    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
