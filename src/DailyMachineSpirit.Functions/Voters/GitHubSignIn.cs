using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Voters;

/// <summary>
/// GitHub's OAuth web flow: the code becomes a token, the token reads the account's id, and then the token is revoked.
/// Nothing asks for a scope, so the token could only ever read public profile data. The code and the token are never
/// logged.
/// </summary>
public sealed class GitHubSignIn : ISignInProvider
{
    public const string AuthorizeEndpoint = "https://github.com/login/oauth/authorize";
    public const string TokenUrl = "https://github.com/login/oauth/access_token";
    public const string UserUrl = "https://api.github.com/user";

    private readonly HttpClient http;
    private readonly IOptions<GitHubOptions> options;
    private readonly ILogger<GitHubSignIn> logger;

    public GitHubSignIn(HttpClient http, IOptions<GitHubOptions> options, ILogger<GitHubSignIn> logger)
    {
        this.http = http;
        this.options = options;
        this.logger = logger;
    }

    public string Name => "github";

    public bool IsConfigured => options.Value.IsConfigured;

    public string AuthorizeUrl(string state)
    {
        var github = options.Value;
        return AuthorizeEndpoint + QueryString.Create(new Dictionary<string, string?>
        {
            ["client_id"] = github.ClientId,
            ["redirect_uri"] = github.CallbackUrl,
            ["state"] = state,
        });
    }

    public async Task<Either<Error, string>> AccountFor(string code, CancellationToken cancellationToken)
    {
        try
        {
            return await AccessToken(code, cancellationToken)
                .BindAsync(accessToken => AccountIdThenRevoke(accessToken, cancellationToken));
        }
        // GitHub unreachable or answering something else: no sign-in. Only the caller's cancellation throws.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.New(ex);
        }
    }

    // Once the exchange starts, GitHub may issue a token at any moment, and only a token read can be revoked: so the
    // reader's cancellation is checked before it, and the client's timeout bounds the exchange itself.
    private async Task<Either<Error, string>> AccessToken(string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var github = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = github.ClientId,
                ["client_secret"] = github.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = github.CallbackUrl,
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(request, CancellationToken.None);
        if (!response.IsSuccessStatusCode)
        {
            return Error.New($"GitHub's token endpoint answered {(int)response.StatusCode}.");
        }

        // A refused code still answers 200, with an error instead of a token.
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        if (answer.TryGetProperty("access_token", out var token) && token.GetString() is { Length: > 0 } value)
        {
            // This app asks for no scope, but GitHub's grants add up: had the reader ever granted it more, the token would
            // carry that again. Such a token is revoked unused, so a token here can only ever read public profile data.
            var scope = answer.TryGetProperty("scope", out var granted) ? granted.GetString() : null;
            if (!string.IsNullOrEmpty(scope))
            {
                await Revoke(value);
                return Error.New($"GitHub granted scopes this site never asks for: {scope}.");
            }
            return value;
        }
        var reason = answer.TryGetProperty("error", out var error) ? error.GetString() : null;
        return Error.New($"GitHub didn't exchange the code for a token: {reason ?? "no token in its answer"}.");
    }

    private async Task<Either<Error, string>> AccountIdThenRevoke(string accessToken, CancellationToken cancellationToken)
    {
        try
        {
            return await AccountId(accessToken, cancellationToken);
        }
        finally
        {
            // Not the request's cancellation: a reader closing the tab mid-sign-in mustn't leave the token valid. The
            // client's timeout bounds it, and the original exception still follows.
            await Revoke(accessToken);
        }
    }

    private async Task<Either<Error, string>> AccountId(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        AddUserAgent(request);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Error.New($"GitHub's user endpoint answered {(int)response.StatusCode}.");
        }

        var user = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return user.TryGetProperty("id", out var id) && id.TryGetInt64(out var accountId)
            ? Right<Error, string>(accountId.ToString(CultureInfo.InvariantCulture))
            : Error.New("GitHub's user endpoint answered without an account id.");
    }

    // Only logged when it fails: the sign-in still counts, since the account id is all it needed, but a token left
    // valid is worth knowing about.
    private async Task Revoke(string accessToken)
    {
        var github = options.Value;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"https://api.github.com/applications/{github.ClientId}/token")
            {
                Content = JsonContent.Create(new { access_token = accessToken }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{github.ClientId}:{github.ClientSecret}")));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            AddUserAgent(request);
            using var response = await http.SendAsync(request, CancellationToken.None);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("A reader's GitHub token couldn't be revoked: GitHub answered {Status}.", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A reader's GitHub token couldn't be revoked: {Reason}", ex.Message);
        }
    }

    // GitHub's API refuses requests without one.
    private static void AddUserAgent(HttpRequestMessage request)
        => request.Headers.UserAgent.Add(new ProductInfoHeaderValue("daily-machine-spirit", "1"));
}
