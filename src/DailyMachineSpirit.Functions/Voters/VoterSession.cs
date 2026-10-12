using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Voters;

/// <summary>
/// Who a signed-in reader is, as far as voting needs: a voter key, an HMAC of their GitHub account id. The same account
/// always gets the same key, so it votes once per rite, but the key never leads back to the account. Their cookie holds
/// the key, until when it's good, and a signature, so nobody can make one up for another key.
/// </summary>
public sealed class VoterSession
{
    public const string CookieName = "dms-voter";
    public static readonly TimeSpan Lasts = TimeSpan.FromDays(30);

    private readonly IOptions<VoterOptions> options;
    private readonly TimeProvider time;

    public VoterSession(IOptions<VoterOptions> options, TimeProvider time)
    {
        this.options = options;
        this.time = time;
    }

    public string VoterKeyFor(long gitHubAccountId)
        => Convert.ToHexStringLower(Hmac(options.Value.KeySecret, $"github:{gitHubAccountId.ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>The cookie's value for a reader who just signed in.</summary>
    public string Issue(string voterKey)
    {
        var expires = time.GetUtcNow().Add(Lasts).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signed = $"{voterKey}.{expires}";
        return $"{signed}.{Signature(signed)}";
    }

    /// <summary>The signed-in reader's voter key, if they have a genuine cookie; None while the secrets aren't set.</summary>
    public Option<string> SignedIn(HttpRequest request)
        => options.Value.IsConfigured && request.Cookies.TryGetValue(CookieName, out var cookie) && cookie is not null
            ? Read(cookie)
            : None;

    /// <summary>The voter key in a cookie that's this app's, unchanged and not expired; None for anything else.</summary>
    public Option<string> Read(string cookie)
    {
        var parts = cookie.Split('.');
        if (parts.Length != 3 || !IsVoterKey(parts[0]) || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires))
        {
            return None;
        }

        var signed = $"{parts[0]}.{parts[1]}";
        var isGenuine = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Signature(signed)), Encoding.ASCII.GetBytes(parts[2]));
        return isGenuine && time.GetUtcNow().ToUnixTimeSeconds() < expires ? Some(parts[0]) : None;
    }

    private string Signature(string signed) => Base64Url.EncodeToString(Hmac(options.Value.SessionSecret, signed));

    private static byte[] Hmac(string secret, string text)
        => HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text));

    private static bool IsVoterKey(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigitLower);
}
