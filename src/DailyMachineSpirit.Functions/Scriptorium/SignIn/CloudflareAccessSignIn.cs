using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// A Scribe is whoever Cloudflare Access signed in: every request it lets through carries a token signed by the team
/// (<see cref="TokenHeader"/>). The app checks the token too, so a request sent straight to the app's own address,
/// around Cloudflare, gets nowhere. The team's signing keys are fetched and kept for an hour, and fetched again early
/// when a token names a key they don't have (Access rotates them), at most every five minutes.
/// </summary>
public sealed class CloudflareAccessSignIn : IScribeSignIn
{
    public const string TokenHeader = "Cf-Access-Jwt-Assertion";
    public const string HttpClientName = "cloudflare-access";

    private static readonly TimeSpan KeysKeptFor = TimeSpan.FromHours(1);
    // A token naming an unknown key fetches the keys again, but at most this often: the app's address is public, and
    // forged tokens with made-up key ids mustn't turn into a stream of fetches that keeps the Scribes waiting.
    private static readonly TimeSpan RefreshAtMostEvery = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory httpClients;
    private readonly IOptions<CloudflareAccessOptions> options;
    private readonly TimeProvider time;
    private readonly ILogger<CloudflareAccessSignIn> logger;
    private readonly SemaphoreSlim fetching = new(1, 1);
    private Option<(JsonWebKeySet Keys, DateTimeOffset FetchedAt)> keys = None;

    public CloudflareAccessSignIn(
        IHttpClientFactory httpClients, IOptions<CloudflareAccessOptions> options, TimeProvider time, ILogger<CloudflareAccessSignIn> logger)
    {
        this.httpClients = httpClients;
        this.options = options;
        this.time = time;
        this.logger = logger;
    }

    public async Task<bool> IsScribe(HttpRequest request, CancellationToken cancellationToken)
    {
        var token = request.Headers[TokenHeader].ToString();
        if (token.Length == 0)
            return false;

        var checkedToken = await Keys(refresh: false, cancellationToken).MapAsync(current => Check(token, current))
            .BindAsync(result => result.Exception is SecurityTokenSignatureKeyNotFoundException
                ? Keys(refresh: true, cancellationToken).MapAsync(fresh => Check(token, fresh))
                : Task.FromResult(Right<Error, TokenValidationResult>(result)));
        return checkedToken.Match(
            Right: result =>
            {
                // The reason only: never the token, which would let anyone who reads the logs act as that Scribe.
                if (!result.IsValid)
                    logger.LogWarning("A Scriptorium request's Access token was refused: {Reason}", result.Exception?.GetType().Name);
                return result.IsValid;
            },
            Left: error =>
            {
                logger.LogError(error.ToException(), "Access's signing keys couldn't be fetched, so nobody is let in: {Reason}", error.Message);
                return false;
            });
    }

    private Task<TokenValidationResult> Check(string token, JsonWebKeySet signingKeys)
    {
        var access = options.Value;
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = $"https://{access.TeamDomain}",
            ValidAudience = access.Audience,
            IssuerSigningKeys = signingKeys.GetSigningKeys(),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        return new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
    }

    private async Task<Either<Error, JsonWebKeySet>> Keys(bool refresh, CancellationToken cancellationToken)
    {
        await fetching.WaitAsync(cancellationToken);
        try
        {
            var now = time.GetUtcNow();
            // Waiting requests that also missed the key find it fetched by the first, so they share one refresh.
            return await keys.Filter(fetched => now - fetched.FetchedAt < (refresh ? RefreshAtMostEvery : KeysKeptFor)).Match(
                Some: fetched => Task.FromResult(Right<Error, JsonWebKeySet>(fetched.Keys)),
                None: () => Fetch(now, cancellationToken));
        }
        finally
        {
            fetching.Release();
        }
    }

    private async Task<Either<Error, JsonWebKeySet>> Fetch(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://{options.Value.TeamDomain}/cdn-cgi/access/certs";
            var json = await httpClients.CreateClient(HttpClientName).GetStringAsync(url, cancellationToken);
            var fetchedKeys = new JsonWebKeySet(json);
            keys = Some((fetchedKeys, now));
            return fetchedKeys;
        }
        // An outage or an answer that isn't a key set: nobody is let in. Only the caller's cancellation throws.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.New(ex);
        }
    }
}
