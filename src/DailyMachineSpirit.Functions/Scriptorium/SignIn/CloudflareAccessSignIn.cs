using DailyMachineSpirit.Data.Entities;
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
    // The app's address is public: forged tokens with made-up key ids, or an Access outage, mustn't turn into a stream
    // of fetches that keeps the Scribes waiting.
    private static readonly TimeSpan MinTimeBetweenFetches = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory httpClients;
    private readonly IOptions<CloudflareAccessOptions> options;
    private readonly TimeProvider time;
    private readonly SiteMetrics metrics;
    private readonly ILogger<CloudflareAccessSignIn> logger;
    private readonly SemaphoreSlim oneFetchAtATime = new(1, 1);
    private Option<(JsonWebKeySet Keys, DateTimeOffset FetchedAt)> cachedKeys = None;
    private Option<DateTimeOffset> lastFailedFetchAt = None;

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

    /// <summary>
    /// The Scribe's Access id (the token's <c>sub</c>), stable for as long as their Access identity is. The record (#9)
    /// keeps it rather than their email.
    /// </summary>
    public async Task<Option<ScribeId>> Scribe(HttpRequest request, CancellationToken cancellationToken)
    {
        var token = request.Headers[TokenHeader].ToString();
        if (token.Length == 0)
        {
            return None;
        }

        var validation = await GetOrFetchKeys(maxAge: KeysKeptFor, cancellationToken)
            .MapAsync(keys => Validate(token, keys));
        if (validation.Exists(SignedWithUnknownKey))
        {
            validation = await GetOrFetchKeys(maxAge: MinTimeBetweenFetches, cancellationToken)
                .MapAsync(keys => Validate(token, keys));
        }

        // Refusals are counted, not logged: the app's address is public, so anyone can send tokens, and a log line for
        // each would let them flood the logs. The reason is the check that failed, never the token itself.
        // A valid token names its Scribe: one that doesn't would leave the record without anyone to name, so it's refused.
        var scribe = validation.ToOption()
            .Filter(result => result.IsValid)
            .Bind(result => Optional(result.Claims.TryGetValue("sub", out var sub) ? sub as string : null))
            .Filter(sub => sub.Length > 0)
            .Map(sub => new ScribeId(sub));
        if (scribe.IsNone)
        {
            metrics.ScribeRefused(validation.Exists(result => result.IsValid) ? "no-subject" : RefusalReason(validation));
        }
        return scribe;
    }

    private Task<TokenValidationResult> Validate(string token, JsonWebKeySet signingKeys)
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

    private static bool SignedWithUnknownKey(TokenValidationResult result) =>
        result.Exception is SecurityTokenSignatureKeyNotFoundException;

    private static string RefusalReason(Either<Error, TokenValidationResult> validation) =>
        validation.Match(
            Right: result => result.Exception?.GetType().Name ?? "invalid",
            Left: _ => "keys-unavailable");

    private async Task<Either<Error, JsonWebKeySet>> GetOrFetchKeys(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        await oneFetchAtATime.WaitAsync(cancellationToken);
        try
        {
            var now = time.GetUtcNow();
            var shouldFetch = !HasKeysFetchedWithin(maxAge, now) && !FetchFailedRecently(now);
            return shouldFetch ? await FetchKeys(now, cancellationToken) : UnexpiredCachedKeys(now);
        }
        finally
        {
            oneFetchAtATime.Release();
        }
    }

    private bool HasKeysFetchedWithin(TimeSpan maxAge, DateTimeOffset now) =>
        cachedKeys.Exists(cached => now - cached.FetchedAt < maxAge);

    private Either<Error, JsonWebKeySet> UnexpiredCachedKeys(DateTimeOffset now) =>
        cachedKeys.Filter(cached => now - cached.FetchedAt < KeysKeptFor).Map(cached => cached.Keys)
            .ToEither(KeysUnavailable);

    private bool FetchFailedRecently(DateTimeOffset now) =>
        lastFailedFetchAt.Exists(failedAt => now - failedAt < MinTimeBetweenFetches);

    private async Task<Either<Error, JsonWebKeySet>> FetchKeys(DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://{options.Value.TeamDomain}/cdn-cgi/access/certs";
            var json = await httpClients.CreateClient(HttpClientName).GetStringAsync(url, cancellationToken);
            var fetchedKeys = new JsonWebKeySet(json);
            cachedKeys = Some((fetchedKeys, now));
            lastFailedFetchAt = None;
            return fetchedKeys;
        }
        // Only the caller's cancellation throws. Logged once per failed fetch, not for every request refused until the
        // next one.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Access's signing keys couldn't be fetched, so nobody is let in for a few minutes: {Reason}", ex.Message);
            lastFailedFetchAt = Some(now);
            return Error.New(ex);
        }
    }
}
