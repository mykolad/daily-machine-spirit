using System.Net;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DailyMachineSpirit.Tests;

public class CloudflareAccessSignInTests
{
    private readonly FakeAccessTeam team = new();
    private readonly FakeTimeProvider time = new(DateTimeOffset.UtcNow);
    private readonly CloudflareAccessSignIn signIn;

    public CloudflareAccessSignInTests()
    {
        signIn = new CloudflareAccessSignIn(
            team,
            Options.Create(new CloudflareAccessOptions { TeamDomain = FakeAccessTeam.Domain, Audience = FakeAccessTeam.Audience }),
            time,
            NullLogger<CloudflareAccessSignIn>.Instance);
    }

    [Fact]
    public async Task ATokenFromTheTeam_ForThisApplication_IsAScribe()
        => Assert.True(await IsScribe(team.ValidToken()));

    [Fact]
    public async Task ARequestWithoutAToken_IsNot()
        => Assert.False(await signIn.IsScribe(new DefaultHttpContext().Request, CancellationToken.None));

    [Fact]
    public async Task ATokenForAnotherApplication_IsNot()
        => Assert.False(await IsScribe(team.Token($"https://{FakeAccessTeam.Domain}", "another-aud-tag", DateTime.UtcNow.AddHours(1))));

    [Fact]
    public async Task ATokenFromAnotherTeam_IsNot()
        => Assert.False(await IsScribe(team.Token("https://elsewhere.cloudflareaccess.com", FakeAccessTeam.Audience, DateTime.UtcNow.AddHours(1))));

    [Fact]
    public async Task AnExpiredToken_IsNot()
        => Assert.False(await IsScribe(team.Token($"https://{FakeAccessTeam.Domain}", FakeAccessTeam.Audience, DateTime.UtcNow.AddMinutes(-5))));

    [Fact]
    public async Task ATokenSignedWithAnotherKey_IsNot()
        => Assert.False(await IsScribe(FakeAccessTeam.ForgedToken()));

    [Fact]
    public async Task ATamperedToken_IsNot()
    {
        var token = team.ValidToken();
        var tampered = token[..^4] + (token[^4..] == "AAAA" ? "BBBB" : "AAAA");

        Assert.False(await IsScribe(tampered));
    }

    [Fact]
    public async Task TheTeamsKeys_AreFetchedOnce_ThenKeptForAnHour()
    {
        Assert.True(await IsScribe(team.ValidToken()));
        Assert.True(await IsScribe(team.ValidToken()));
        Assert.Equal(1, team.KeyFetches);

        time.Advance(TimeSpan.FromMinutes(61));
        Assert.True(await IsScribe(team.ValidToken()));
        Assert.Equal(2, team.KeyFetches);
    }

    [Fact]
    public async Task AfterTheTeamRotatesItsKey_TheNewKeysAreFetchedBeforeTheHourIsOut()
    {
        Assert.True(await IsScribe(team.ValidToken()));
        team.RotateKey();
        time.Advance(TimeSpan.FromMinutes(6));

        Assert.True(await IsScribe(team.ValidToken()));
        Assert.Equal(2, team.KeyFetches);
    }

    [Fact]
    public async Task ForgedTokens_CantMakeTheKeysBeFetchedAgainAndAgain()
    {
        Assert.True(await IsScribe(team.ValidToken()));

        for (var i = 0; i < 10; i++)
            Assert.False(await IsScribe(FakeAccessTeam.ForgedToken()));

        Assert.Equal(1, team.KeyFetches);
        time.Advance(TimeSpan.FromMinutes(6));
        Assert.False(await IsScribe(FakeAccessTeam.ForgedToken()));
        Assert.Equal(2, team.KeyFetches);
    }

    [Fact]
    public async Task WhenTheKeysCantBeFetched_NobodyIsLetIn()
    {
        team.KeysStatus = HttpStatusCode.ServiceUnavailable;

        Assert.False(await IsScribe(team.ValidToken()));
    }

    private Task<bool> IsScribe(string token)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[CloudflareAccessSignIn.TokenHeader] = token;
        return signIn.IsScribe(request, CancellationToken.None);
    }
}
