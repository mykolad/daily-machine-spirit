using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// A Cloudflare Access team: it signs tokens with its current key and serves its public keys at
/// <c>/cdn-cgi/access/certs</c>, counting how often they're fetched. Rotating swaps the key.
/// </summary>
public sealed class FakeAccessTeam : HttpMessageHandler, IHttpClientFactory
{
    public const string Domain = "scribes.cloudflareaccess.com";
    public const string Audience = "the-scriptoriums-aud-tag";

    /// <summary>The signed-in Scribe's Access id: what a token's <c>sub</c> carries.</summary>
    public const string ScribeId = "7335d417-61da-459d-899c-0a1f8f6f0d3b";

    private RsaSecurityKey key = NewKey("first");

    public int KeyFetches { get; private set; }

    public HttpStatusCode KeysStatus { get; set; } = HttpStatusCode.OK;

    public void RotateKey() => key = NewKey("second");

    /// <summary>A token as Access issues it, for this team and audience unless told otherwise.</summary>
    public string Token(string issuer, string audience, DateTime expires)
        => Token(issuer, audience, expires, new Dictionary<string, object> { ["sub"] = ScribeId, ["email"] = "scribe@example.com" });

    /// <summary>A valid token that names nobody: no <c>sub</c>.</summary>
    public string TokenWithoutSubject()
        => Token($"https://{Domain}", Audience, DateTime.UtcNow.AddHours(1), new Dictionary<string, object> { ["email"] = "scribe@example.com" });

    public string ValidToken() => Token($"https://{Domain}", Audience, DateTime.UtcNow.AddHours(1));

    private string Token(string issuer, string audience, DateTime expires, Dictionary<string, object> claims)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = expires.AddHours(-2),
            IssuedAt = expires.AddHours(-2),
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });

    /// <summary>A token signed with a key the team never published.</summary>
    public static string ForgedToken()
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = $"https://{Domain}",
            Audience = Audience,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(NewKey("forged"), SecurityAlgorithms.RsaSha256),
        });

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.RequestUri?.ToString() != $"https://{Domain}/cdn-cgi/access/certs")
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
        KeyFetches++;
        var parameters = key.Rsa.ExportParameters(includePrivateParameters: false);
        var jwks = $$"""
            {"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"{{key.KeyId}}","n":"{{Base64UrlEncoder.Encode(parameters.Modulus)}}","e":"{{Base64UrlEncoder.Encode(parameters.Exponent)}}"}]}
            """;
        return Task.FromResult(new HttpResponseMessage(KeysStatus) { Content = new StringContent(jwks, Encoding.UTF8, "application/json") });
    }

    private static RsaSecurityKey NewKey(string id) => new(RSA.Create(2048)) { KeyId = id };
}
