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
/// Readers sign in with GitHub to vote: <c>/auth/github</c> sends them there, GitHub sends them back to the callback,
/// and the <see cref="VoterSession"/> cookie keeps them signed in. Separate from the Scriptorium, whose Scribes are
/// whoever Cloudflare Access let in: signing in here makes nobody a Scribe. Until the GitHub app and the voters'
/// secrets are set, the sign-in pages don't exist and nobody is signed in.
/// </summary>
public sealed class SignInFunction
{
    public const string StateCookieName = "dms-signin-state";
    public const string AuthorizeUrl = "https://github.com/login/oauth/authorize";

    // Long enough to sign in on GitHub, short enough that an old state is useless.
    private static readonly TimeSpan StateLasts = TimeSpan.FromMinutes(10);
    private const int MaxReturnPathLength = 512;

    private readonly IGitHubAccounts accounts;
    private readonly VoterSession session;
    private readonly IOptions<GitHubOptions> github;
    private readonly IOptions<VoterOptions> voters;
    private readonly SiteMetrics metrics;
    private readonly ILogger<SignInFunction> logger;

    public SignInFunction(
        IGitHubAccounts accounts,
        VoterSession session,
        IOptions<GitHubOptions> github,
        IOptions<VoterOptions> voters,
        SiteMetrics metrics,
        ILogger<SignInFunction> logger)
    {
        this.accounts = accounts;
        this.session = session;
        this.github = github;
        this.voters = voters;
        this.metrics = metrics;
        this.logger = logger;
    }

    /// <summary>Off to GitHub, remembering where to come back to (<c>?return=/r/12</c>).</summary>
    [Function("SignIn")]
    public IActionResult SignIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/github")] HttpRequest request)
    {
        NeverCached(request);
        if (!IsConfigured)
        {
            return new NotFoundResult();
        }

        // The state ties GitHub's answer to this browser: a callback whose state isn't the one in its cookie was started
        // somewhere else, perhaps to sign the reader in as someone else.
        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var returnPath = SafeReturnPath(request.Query["return"].ToString());
        request.HttpContext.Response.Cookies.Append(StateCookieName, $"{state}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(returnPath))}",
            Cookie(StateLasts));

        var options = github.Value;
        var query = QueryString.Create(new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.CallbackUrl,
            ["state"] = state,
        });
        return Redirect($"{AuthorizeUrl}{query}");
    }

    [Function("SignInCallback")]
    public async Task<IActionResult> Callback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/github/callback")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        NeverCached(request);
        if (!IsConfigured)
        {
            return new NotFoundResult();
        }

        var started = StartedHere(request);
        request.HttpContext.Response.Cookies.Delete(StateCookieName, Cookie(TimeSpan.Zero));
        var returnPath = started.Map(state => state.ReturnPath).IfNone("/");
        var stateMatches = started.Exists(state => FixedTimeEquals(state.State, request.Query["state"].ToString()));
        if (!stateMatches)
        {
            metrics.SignIn("refused");
            return new BadRequestResult();
        }

        // Back where they were, signed out, when the reader said no on GitHub ("access_denied"). Any other error, such as
        // redirect_uri_mismatch or a suspended app, is the site's own setup failing, so it's counted and logged as one.
        var error = request.Query["error"].ToString();
        if (error == "access_denied")
        {
            metrics.SignIn("declined");
            return Redirect(returnPath);
        }
        var code = request.Query["code"].ToString();
        if (error.Length > 0 || code.Length == 0)
        {
            logger.LogWarning("GitHub sent a reader back without a sign-in: {Error}", error.Length > 0 ? error : "no code");
            metrics.SignIn("failed");
            return Redirect(returnPath);
        }

        return (await accounts.AccountFor(code, cancellationToken)).Match(
            Right: accountId =>
            {
                request.HttpContext.Response.Cookies.Append(VoterSession.CookieName, session.Issue(session.VoterKeyFor(accountId)),
                    Cookie(VoterSession.Lasts));
                metrics.SignIn("signed-in");
                return Redirect(returnPath);
            },
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "A reader couldn't sign in with GitHub: {Reason}", error.Message);
                metrics.SignIn("failed");
                return Redirect(returnPath);
            });
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
        return new OkObjectResult(new { signInAvailable = IsConfigured, signedIn = session.SignedIn(request).IsSome });
    }

    private bool IsConfigured => github.Value.IsConfigured && voters.Value.IsConfigured;

    private static Option<(string State, string ReturnPath)> StartedHere(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(StateCookieName, out var cookie) || cookie is null)
        {
            return None;
        }
        var parts = cookie.Split('.');
        if (parts.Length != 2 || parts[0].Length == 0)
        {
            return None;
        }
        try
        {
            return Some((parts[0], SafeReturnPath(Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[1])))));
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
        // Lax: sent when GitHub sends the reader back (a top-level GET), never with another site's posts.
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = lasts,
    };

    private static void NeverCached(HttpRequest request) => request.HttpContext.Response.Headers.CacheControl = "no-store";

    private static IActionResult Redirect(string url) => new RedirectResult(url) { PreserveMethod = false, Permanent = false };

    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
