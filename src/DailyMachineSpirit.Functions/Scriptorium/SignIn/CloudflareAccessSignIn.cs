using DailyMachineSpirit.Functions.Telemetry;
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
/// when a token names a key they don't have (Access rotates them), at most every five minutes. A failed fetch waits as
/// long before the next.
/// </summary>
public sealed class CloudflareAccessSignIn : IScribeSignIn
{
    public const string TokenHeader = "Cf-Access-Jwt-Assertion";
    public const string HttpClientName = "cloudflare-access";

    public static readonly Error KeysUnavailable = Error.New("Access's signing keys couldn't be fetched lately; the next try waits a few minutes.");

    private static readonly TimeSpan KeysKeptFor = TimeSpan.FromHours(1);
    // A token naming an unknown key fetches the keys again, but at most this often: the app's address is public, and
    // forged tokens with made-up key ids mustn't turn into a stream of fetches that keeps the Scribes waiting.
    private static readonly TimeSpan RefreshAtMostEvery = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory httpClients;
    private readonly IOptions<CloudflareAccessOptions> options;
    private readonly TimeProvider time;
    private readonly SiteMetrics metrics;
    private readonly ILogger<CloudflareAccessSignIn> logger;
    private readonly SemaphoreSlim fetching = new(1, 1);
    private Option<(JsonWebKeySet Keys, DateTimeOffset FetchedAt)> keys = None;
    private Option<DateTimeOffset> lastFailure = None;

    public CloudflareAccessSignIn(
        IHttpClientFactory httpClients,
        IOptions<CloudflareAccessOptions> options,
        TimeProvider time,
        SiteMetrics metrics,
        ILogger<CloudflareAccessSignIn> logger)
    {
        this.httpClients = httpClients;
        this.options = options;
        this.time = time;
        this.metrics = metrics;
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
        // Refusals are counted, not logged: the app's address is public, so anyone can send tokens, and a log line for
        // each would let them flood the logs. The reason is the check that failed, never the token itself.
        var isScribe = checkedToken.Match(Right: result => result.IsValid, Left: _ => false);
        if (!isScribe)
            metrics.ScribeRefused(checkedToken.Match(
                Right: result => result.Exception?.GetType().Name ?? "invalid",
                Left: _ => "keys-unavailable"));
        return isScribe;
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
            var fresh = keys.Filter(fetched => now - fetched.FetchedAt < (refresh ? RefreshAtMostEvery : KeysKeptFor));
            // After a failed fetch, the next waits as long as a refresh would: during an Access outage, each request
            // starting its own fetch would keep the Scribes waiting behind them. Keys still within their hour still serve.
            var coolingDown = lastFailure.Filter(failed => now - failed < RefreshAtMostEvery).IsSome;
            return await fresh.Match(
                Some: fetched => Task.FromResult(Right<Error, JsonWebKeySet>(fetched.Keys)),
                None: () => coolingDown
                    ? Task.FromResult(keys.Filter(fetched => now - fetched.FetchedAt < KeysKeptFor).Map(fetched => fetched.Keys)
                        .ToEither(KeysUnavailable))
                    : Fetch(now, cancellationToken));
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
            lastFailure = None;
            return fetchedKeys;
        }
        // An outage or an answer that isn't a key set: nobody is let in. Only the caller's cancellation throws. Logged
        // here, once per failed fetch, rather than for every request refused during the wait before the next.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Access's signing keys couldn't be fetched, so nobody is let in for a few minutes: {Reason}", ex.Message);
            lastFailure = Some(now);
            return Error.New(ex);
        }
    }
}
