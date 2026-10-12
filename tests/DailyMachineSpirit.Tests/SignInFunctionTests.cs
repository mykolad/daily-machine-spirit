using DailyMachineSpirit.Functions.Voters;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class SignInFunctionTests : IDisposable
{
    private const long AccountId = 583231;

    private readonly MetricsProbe probe = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));
    private readonly FakeGitHubAccounts accounts = new(Right<Error, long>(AccountId));

    public void Dispose() => probe.Dispose();

    [Fact]
    public void SignIn_SendsTheReaderToGitHub_WithTheAppAndAState()
    {
        var request = Get("/auth/github", "?return=/r/12", string.Empty);

        var redirect = Assert.IsType<RedirectResult>(Function().SignIn(request));

        Assert.StartsWith(SignInFunction.AuthorizeUrl + "?", redirect.Url);
        var query = QueryHelpers.ParseQuery(new Uri(redirect.Url).Query);
        Assert.Equal("Iv1.client", query["client_id"].ToString());
        Assert.Equal("https://staging.example/auth/github/callback", query["redirect_uri"].ToString());
        var stateCookie = SetCookie(request, SignInFunction.StateCookieName);
        Assert.StartsWith($"{query["state"]}.", stateCookie.Value);
        Assert.Contains("httponly", stateCookie.Attributes);
        Assert.Contains("secure", stateCookie.Attributes);
        Assert.Contains("samesite=lax", stateCookie.Attributes);
        Assert.Equal("no-store", request.HttpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Callback_SignsTheReaderIn_AndSendsThemBack()
    {
        using var signIns = probe.Collect<long>("dms.signins");
        var (state, stateCookie) = StartSignIn("/r/12");
        var request = Get("/auth/github/callback", $"?code=the-code&state={state}", stateCookie);

        var result = await Function().Callback(request, CancellationToken.None);

        Assert.Equal("/r/12", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal(["the-code"], accounts.Codes);
        var voter = SetCookie(request, VoterSession.CookieName);
        Assert.Equal(Some(Session().VoterKeyFor(AccountId)), Session().Read(voter.Value));
        Assert.Contains("httponly", voter.Attributes);
        Assert.Equal(string.Empty, SetCookie(request, SignInFunction.StateCookieName).Value);
        Assert.Equal(["outcome=signed-in"], MetricsProbe.Tags(signIns, "outcome"));
    }

    [Theory]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    [InlineData("https://evil.example/")]
    [InlineData("")]
    public async Task Callback_SendsTheReaderOnlyToAPathOnThisSite(string returnTo)
    {
        var (state, stateCookie) = StartSignIn(returnTo);

        var result = await Function().Callback(Get("/auth/github/callback", $"?code=the-code&state={state}", stateCookie),
            CancellationToken.None);

        Assert.Equal("/", Assert.IsType<RedirectResult>(result).Url);
    }

    [Theory]
    [InlineData("another-state", true)]
    [InlineData("", true)]
    [InlineData("matching", false)]
    public async Task Callback_NotStartedInThisBrowser_IsRefused(string state, bool withStateCookie)
    {
        using var signIns = probe.Collect<long>("dms.signins");
        var (started, stateCookie) = StartSignIn("/");
        var query = $"?code=the-code&state={(state == "matching" ? started : state)}";
        var request = Get("/auth/github/callback", query, withStateCookie ? stateCookie : string.Empty);

        var result = await Function().Callback(request, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(accounts.Codes);
        Assert.False(HasSetCookie(request, VoterSession.CookieName));
        Assert.Equal(["outcome=refused"], MetricsProbe.Tags(signIns, "outcome"));
    }

    [Theory]
    [InlineData("no-separator")]
    [InlineData(".no-state")]
    [InlineData("state.not*base64")]
    public async Task Callback_WithAMangledStateCookie_IsRefused(string stateCookie)
    {
        var request = Get("/auth/github/callback", "?code=the-code&state=state", $"{SignInFunction.StateCookieName}={stateCookie}");

        Assert.IsType<BadRequestResult>(await Function().Callback(request, CancellationToken.None));
        Assert.Empty(accounts.Codes);
    }

    [Fact]
    public async Task Callback_WhenTheReaderDeclinesOnGitHub_SendsThemBack_SignedOut()
    {
        using var signIns = probe.Collect<long>("dms.signins");
        var (state, stateCookie) = StartSignIn("/archive");
        var request = Get("/auth/github/callback", $"?error=access_denied&state={state}", stateCookie);

        var result = await Function().Callback(request, CancellationToken.None);

        Assert.Equal("/archive", Assert.IsType<RedirectResult>(result).Url);
        Assert.Empty(accounts.Codes);
        Assert.False(HasSetCookie(request, VoterSession.CookieName));
        Assert.Equal(["outcome=declined"], MetricsProbe.Tags(signIns, "outcome"));
    }

    [Theory]
    [InlineData("?error=redirect_uri_mismatch")]
    [InlineData("?error=application_suspended")]
    [InlineData("")]
    public async Task Callback_WhenGitHubSendsAnErrorOrNoCode_CountsAFailure_NotTheReadersChoice(string answer)
    {
        using var signIns = probe.Collect<long>("dms.signins");
        var (state, stateCookie) = StartSignIn("/archive");
        var query = answer.Length > 0 ? $"{answer}&state={state}" : $"?state={state}";

        var result = await Function().Callback(Get("/auth/github/callback", query, stateCookie), CancellationToken.None);

        Assert.Equal("/archive", Assert.IsType<RedirectResult>(result).Url);
        Assert.Empty(accounts.Codes);
        Assert.Equal(["outcome=failed"], MetricsProbe.Tags(signIns, "outcome"));
    }

    [Fact]
    public async Task Callback_WhenGitHubFails_SendsTheReaderBack_SignedOut()
    {
        using var signIns = probe.Collect<long>("dms.signins");
        var failing = new FakeGitHubAccounts(Left<Error, long>(Error.New("GitHub is down.")));
        var (state, stateCookie) = StartSignIn("/archive");
        var request = Get("/auth/github/callback", $"?code=the-code&state={state}", stateCookie);

        var result = await Function(failing).Callback(request, CancellationToken.None);

        Assert.Equal("/archive", Assert.IsType<RedirectResult>(result).Url);
        Assert.False(HasSetCookie(request, VoterSession.CookieName));
        Assert.Equal(["outcome=failed"], MetricsProbe.Tags(signIns, "outcome"));
    }

    [Theory]
    [InlineData("", "client-secret", "key-secret")]
    [InlineData("Iv1.client", "@Microsoft.KeyVault(VaultName=v;SecretName=GitHubClientSecret)", "key-secret")]
    [InlineData("Iv1.client", "client-secret", "")]
    public async Task WithoutTheSettings_NobodySignsIn(string clientId, string clientSecret, string keySecret)
    {
        var function = Function(accounts, GitHub(clientId, clientSecret), Voters(keySecret));

        Assert.IsType<NotFoundResult>(function.SignIn(Get("/auth/github", string.Empty, string.Empty)));
        Assert.IsType<NotFoundResult>(await function.Callback(Get("/auth/github/callback", "?code=c&state=s", string.Empty), CancellationToken.None));
        Assert.Empty(accounts.Codes);
        Assert.False(MeSays(function, Get("/api/me", string.Empty, string.Empty), "signInAvailable"));
    }

    [Fact]
    public void Me_SaysWhetherTheReaderIsSignedIn()
    {
        var genuine = Session().Issue(Session().VoterKeyFor(AccountId));
        var forged = $"{Session().VoterKeyFor(1)}.{genuine.Split('.')[1]}.{genuine.Split('.')[2]}";

        Assert.True(SignedIn(Get("/api/me", string.Empty, $"{VoterSession.CookieName}={genuine}")));
        Assert.False(SignedIn(Get("/api/me", string.Empty, $"{VoterSession.CookieName}={forged}")));
        Assert.False(SignedIn(Get("/api/me", string.Empty, string.Empty)));
        Assert.True(MeSays(Function(), Get("/api/me", string.Empty, string.Empty), "signInAvailable"));
    }

    [Fact]
    public void SignOut_ForgetsTheReader_AndSendsThemBack()
    {
        var request = SignOutPost("same-origin");

        var result = Function().SignOut(request);

        Assert.Equal("/r/12", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal(string.Empty, SetCookie(request, VoterSession.CookieName).Value);
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    [InlineData("")]
    public void SignOut_PostedFromAnotherSite_IsRefused(string fetchSite)
    {
        var request = SignOutPost(fetchSite);

        var result = Function().SignOut(request);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.False(HasSetCookie(request, VoterSession.CookieName));
    }

    private HttpRequest SignOutPost(string fetchSite)
    {
        var request = Get("/auth/signout", "?return=/r/12", $"{VoterSession.CookieName}={Session().Issue(Session().VoterKeyFor(AccountId))}");
        request.Method = "POST";
        if (fetchSite.Length > 0)
        {
            request.Headers["Sec-Fetch-Site"] = fetchSite;
        }
        return request;
    }

    private bool SignedIn(HttpRequest request) => MeSays(Function(), request, "signedIn");

    private static bool MeSays(SignInFunction function, HttpRequest request, string property)
    {
        var result = Assert.IsType<OkObjectResult>(function.Me(request));
        Assert.Equal("no-store", request.HttpContext.Response.Headers.CacheControl.ToString());
        return (bool)(result.Value?.GetType().GetProperty(property)?.GetValue(result.Value) ?? false);
    }

    // Starts a sign-in the way a browser would, and returns the state GitHub would send back and the cookie it would keep.
    private (string State, string Cookie) StartSignIn(string returnTo)
    {
        var request = Get("/auth/github", $"?return={Uri.EscapeDataString(returnTo)}", string.Empty);
        var redirect = Assert.IsType<RedirectResult>(Function().SignIn(request));
        var state = QueryHelpers.ParseQuery(new Uri(redirect.Url).Query)["state"].ToString();
        return (state, $"{SignInFunction.StateCookieName}={SetCookie(request, SignInFunction.StateCookieName).Value}");
    }

    private SignInFunction Function() => Function(accounts);

    private SignInFunction Function(FakeGitHubAccounts gitHubAccounts)
        => Function(gitHubAccounts, GitHub("Iv1.client", "client-secret"), Voters("key-secret"));

    private SignInFunction Function(FakeGitHubAccounts gitHubAccounts, GitHubOptions gitHub, VoterOptions voters)
        => new(gitHubAccounts, new VoterSession(Options.Create(voters), time), Options.Create(gitHub), Options.Create(voters),
            probe.Metrics, NullLogger<SignInFunction>.Instance);

    private VoterSession Session() => new(Options.Create(Voters("key-secret")), time);

    private static GitHubOptions GitHub(string clientId, string clientSecret) => new()
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
        CallbackUrl = "https://staging.example/auth/github/callback",
    };

    private static VoterOptions Voters(string keySecret) => new() { KeySecret = keySecret, SessionSecret = "session-secret" };

    private static HttpRequest Get(string path, string query, string cookie)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "GET";
        request.Path = path;
        request.QueryString = new QueryString(query);
        if (cookie.Length > 0)
        {
            request.Headers.Cookie = cookie;
        }
        return request;
    }

    private static bool HasSetCookie(HttpRequest request, string name)
        => request.HttpContext.Response.Headers.SetCookie.Any(header => header?.StartsWith($"{name}=", StringComparison.Ordinal) == true);

    // The cookie the response sets, with its attributes lowercased; an empty value is a deleted cookie.
    private static (string Value, List<string> Attributes) SetCookie(HttpRequest request, string name)
    {
        var header = request.HttpContext.Response.Headers.SetCookie
            .Last(header => header?.StartsWith($"{name}=", StringComparison.Ordinal) == true) ?? string.Empty;
        var parts = header.Split(';', StringSplitOptions.TrimEntries);
        return (parts[0][(name.Length + 1)..], parts.Skip(1).Select(part => part.ToLowerInvariant()).ToList());
    }
}
