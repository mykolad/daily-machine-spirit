using System.Net;
using System.Text;
using DailyMachineSpirit.Functions.Voters;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public class GitHubAccountsTests
{
    private static readonly GitHubOptions Options = new()
    {
        ClientId = "Iv1.client",
        ClientSecret = "client-secret",
        CallbackUrl = "https://staging.example/auth/github/callback",
    };

    private readonly FakeGitHub github = new();

    [Fact]
    public async Task AccountFor_ExchangesTheCode_AndReadsTheAccountId()
    {
        var accountId = Ok(await Accounts().AccountFor("the-code", CancellationToken.None));

        Assert.Equal(583231, accountId);
        var exchange = github.Requests[0];
        Assert.Equal((HttpMethod.Post, GitHubAccounts.TokenUrl), (exchange.Method, exchange.Url));
        Assert.Equal("client_id=Iv1.client&client_secret=client-secret&code=the-code&redirect_uri=https%3A%2F%2Fstaging.example%2Fauth%2Fgithub%2Fcallback",
            exchange.Body);
        Assert.Equal("application/json", exchange.Request.Headers.Accept.ToString());
        var user = github.Requests[1];
        Assert.Equal((HttpMethod.Get, GitHubAccounts.UserUrl), (user.Method, user.Url));
        Assert.Equal($"Bearer {FakeGitHub.Token}", user.Request.Headers.Authorization?.ToString());
        Assert.NotEmpty(user.Request.Headers.UserAgent);
    }

    [Fact]
    public async Task AccountFor_RevokesTheToken_OnceItHasTheId()
    {
        Ok(await Accounts().AccountFor("the-code", CancellationToken.None));

        var revoke = github.Requests[2];
        Assert.Equal((HttpMethod.Delete, "https://api.github.com/applications/Iv1.client/token"), (revoke.Method, revoke.Url));
        Assert.Equal($$"""{"access_token":"{{FakeGitHub.Token}}"}""", revoke.Body);
        Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("Iv1.client:client-secret"))}",
            revoke.Request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task AccountFor_WhenGitHubRefusesTheCode_ReturnsAnError_AndAsksNothingMore()
    {
        github.TokenAnswer = (HttpStatusCode.OK, """{"error":"bad_verification_code","error_description":"The code passed is incorrect or expired."}""");

        var error = Failed(await Accounts().AccountFor("an-old-code", CancellationToken.None));

        Assert.Contains("bad_verification_code", error.Message);
        Assert.Single(github.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}""")]
    [InlineData(HttpStatusCode.OK, """{"login":"octocat"}""")]
    public async Task AccountFor_WithoutAnAccountId_ReturnsAnError_AndStillRevokesTheToken(HttpStatusCode status, string body)
    {
        github.UserAnswer = (status, body);

        Failed(await Accounts().AccountFor("the-code", CancellationToken.None));

        Assert.Contains(github.Requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task AccountFor_WhenTheReaderLeavesMidSignIn_StillRevokesTheToken()
    {
        using var readerLeaves = new CancellationTokenSource();
        github.AfterTokenExchange = readerLeaves.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Accounts().AccountFor("the-code", readerLeaves.Token));

        Assert.Contains(github.Requests, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task AccountFor_WhenTheTokenCantBeRevoked_StillSignsIn()
    {
        github.RevokeStatus = HttpStatusCode.InternalServerError;

        Assert.Equal(583231, Ok(await Accounts().AccountFor("the-code", CancellationToken.None)));
    }

    [Fact]
    public async Task AccountFor_WhenGitHubIsDown_ReturnsAnError()
    {
        github.TokenAnswer = (HttpStatusCode.ServiceUnavailable, "<html>");

        Failed(await Accounts().AccountFor("the-code", CancellationToken.None));
    }

    [Fact]
    public async Task AccountFor_WhenCancelled_Throws()
        => await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Accounts().AccountFor("the-code", new CancellationToken(canceled: true)));

    private GitHubAccounts Accounts()
        => new(new HttpClient(github), Microsoft.Extensions.Options.Options.Create(Options), NullLogger<GitHubAccounts>.Instance);
}
