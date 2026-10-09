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

    private RsaSecurityKey key = NewKey("first");

    public int KeyFetches { get; private set; }

    public HttpStatusCode KeysStatus { get; set; } = HttpStatusCode.OK;

    public void RotateKey() => key = NewKey("second");

    /// <summary>A token as Access issues it, for this team and audience unless told otherwise.</summary>
    public string Token(string issuer, string audience, DateTime expires)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = expires.AddHours(-2),
            IssuedAt = expires.AddHours(-2),
            Expires = expires,
            Claims = new Dictionary<string, object> { ["email"] = "scribe@example.com" },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256),
        });

    public string ValidToken() => Token($"https://{Domain}", Audience, DateTime.UtcNow.AddHours(1));

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
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        KeyFetches++;
        var parameters = key.Rsa.ExportParameters(includePrivateParameters: false);
        var jwks = $$"""
            {"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"{{key.KeyId}}","n":"{{Base64UrlEncoder.Encode(parameters.Modulus)}}","e":"{{Base64UrlEncoder.Encode(parameters.Exponent)}}"}]}
            """;
        return Task.FromResult(new HttpResponseMessage(KeysStatus) { Content = new StringContent(jwks, Encoding.UTF8, "application/json") });
    }

    private static RsaSecurityKey NewKey(string id) => new(RSA.Create(2048)) { KeyId = id };
}
