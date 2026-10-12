using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Functions.Voters;

/// <summary>
/// GitHub's OAuth web flow: the code becomes a token, the token reads the account's id, and then the token is revoked.
/// Nothing asks for a scope, so the token could only ever read public profile data. The code and the token are never
/// logged.
/// </summary>
public sealed class GitHubAccounts : IGitHubAccounts
{
    public const string TokenUrl = "https://github.com/login/oauth/access_token";
    public const string UserUrl = "https://api.github.com/user";

    private readonly HttpClient http;
    private readonly IOptions<GitHubOptions> options;
    private readonly ILogger<GitHubAccounts> logger;

    public GitHubAccounts(HttpClient http, IOptions<GitHubOptions> options, ILogger<GitHubAccounts> logger)
    {
        this.http = http;
        this.options = options;
        this.logger = logger;
    }

    public async Task<Either<Error, long>> AccountFor(string code, CancellationToken cancellationToken)
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
            return value;
        }
        var reason = answer.TryGetProperty("error", out var error) ? error.GetString() : null;
        return Error.New($"GitHub didn't exchange the code for a token: {reason ?? "no token in its answer"}.");
    }

    private async Task<Either<Error, long>> AccountIdThenRevoke(string accessToken, CancellationToken cancellationToken)
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

    private async Task<Either<Error, long>> AccountId(string accessToken, CancellationToken cancellationToken)
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
            ? accountId
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
