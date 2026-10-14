using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DailyMachineSpirit.SmokeTests;

/// <summary>
/// A voter the smoke tests sign in as, with the cookie the app issues after a sign-in (<c>VoterSession</c>): the voter
/// key, until when it's good, and its signature with the session secret in <see cref="SecretVariable"/>. Only where the
/// workflow sets it (staging): production's counts are public.
/// </summary>
public static class TestVoter
{
    public const string SecretVariable = "SMOKE_TEST_VOTER_SESSION_SECRET";
    public const string CookieName = "dms-voter";

    // Not an HMAC of an account like a real reader's key, so no reader ever has it: a SHA-256, in the same form.
    private static readonly string VoterKey =
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("smoke-test voter")));

    public static bool IsAvailable => Environment.GetEnvironmentVariable(SecretVariable) is { Length: > 0 };

    /// <summary>The cookie's value, good for an hour.</summary>
    public static string Cookie()
    {
        var secret = Environment.GetEnvironmentVariable(SecretVariable)
            ?? throw new InvalidOperationException($"Set {SecretVariable} to vote as the test voter.");
        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signed = $"{VoterKey}.{expires}";
        var signature = Base64Url.EncodeToString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed)));
        return $"{signed}.{signature}";
    }
}
