using DailyMachineSpirit.Functions.Voters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public class VoterSessionTests
{
    private static readonly VoterOptions Secrets = WithSecrets("key-secret-of-at-least-32-characters", "session-secret-of-at-least-32-characters");

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));

    [Fact]
    public void VoterKeyFor_IsTheSameForAnAccount_AndDiffersBetweenAccounts()
    {
        var session = Session(Secrets);

        Assert.Equal(session.VoterKeyFor("github", "583231"), session.VoterKeyFor("github", "583231"));
        Assert.NotEqual(session.VoterKeyFor("github", "583231"), session.VoterKeyFor("github", "583232"));
        Assert.Matches("^[0-9a-f]{64}$", session.VoterKeyFor("github", "583231"));
    }

    [Fact]
    public void VoterKeyFor_DiffersBetweenProviders_ForTheSameId()
        => Assert.NotEqual(Session(Secrets).VoterKeyFor("github", "583231"), Session(Secrets).VoterKeyFor("google", "583231"));

    [Fact]
    public void VoterKeyFor_DependsOnTheKeySecret_SoItCantBeWorkedOutFromTheId()
        => Assert.NotEqual(Session(Secrets).VoterKeyFor("github", "583231"), Session(WithSecrets("another-secret-of-at-least-32-characters", "session-secret-of-at-least-32-characters")).VoterKeyFor("github", "583231"));

    [Fact]
    public void Read_GivesBackTheVoterKey_OfACookieItIssued()
    {
        var session = Session(Secrets);
        var key = session.VoterKeyFor("github", "583231");

        Assert.Equal(Some(key), session.Read(session.Issue(key)));
    }

    [Fact]
    public void Read_RefusesACookie_ForAnotherKey()
    {
        var session = Session(Secrets);
        var issued = session.Issue(session.VoterKeyFor("github", "583231")).Split('.');

        Assert.Equal(None, session.Read($"{session.VoterKeyFor("github", "1")}.{issued[1]}.{issued[2]}"));
    }

    [Fact]
    public void Read_RefusesACookie_SignedWithAnotherSecret()
    {
        var other = Session(WithSecrets("key-secret-of-at-least-32-characters", "another-secret-of-at-least-32-characters"));

        Assert.Equal(None, Session(Secrets).Read(other.Issue(other.VoterKeyFor("github", "583231"))));
    }

    [Fact]
    public void Read_RefusesACookie_OnceItHasExpired()
    {
        var session = Session(Secrets);
        var cookie = session.Issue(session.VoterKeyFor("github", "583231"));

        time.Advance(VoterSession.Lasts);

        Assert.Equal(None, session.Read(cookie));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cookie")]
    [InlineData("abc.123.sig")]
    [InlineData("a.b.c.d")]
    public void Read_RefusesAnythingElse(string cookie) => Assert.Equal(None, Session(Secrets).Read(cookie));

    [Fact]
    public void SignedIn_ReadsTheCookie_OnlyWhileTheSecretsAreSet()
    {
        var session = Session(Secrets);
        var request = new DefaultHttpContext().Request;
        request.Headers.Cookie = $"{VoterSession.CookieName}={session.Issue(session.VoterKeyFor("github", "583231"))}";

        Assert.True(session.SignedIn(request).IsSome);
        Assert.True(Session(WithSecrets("key-secret-of-at-least-32-characters", "@Microsoft.KeyVault(VaultName=v;SecretName=s)")).SignedIn(request).IsNone);
    }

    [Theory]
    [InlineData("k", "session-secret-of-at-least-32-characters")]
    [InlineData("key-secret-of-at-least-32-characters", "short-session-secret")]
    [InlineData("key-secret-of-at-least-32-characters", "                                  s")]
    public void ShortSecrets_CountAsNotSet_SoNobodySignsIn(string keySecret, string sessionSecret)
        => Assert.False(WithSecrets(keySecret, sessionSecret).IsConfigured);

    private VoterSession Session(VoterOptions secrets) => new(Options.Create(secrets), time);

    private static VoterOptions WithSecrets(string keySecret, string sessionSecret)
        => new() { KeySecret = keySecret, SessionSecret = sessionSecret };
}
