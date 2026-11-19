using System.Text;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Api;
using DailyMachineSpirit.Functions.Voters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

// A signed-in reader's witnessed rites (#215), kept with their account so they follow the reader to every browser.
public sealed class WitnessedTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();
    private readonly VoterSession session = new(
        Options.Create(new VoterOptions
        {
            KeySecret = "key-secret-of-at-least-32-characters",
            SessionSecret = "session-secret-of-at-least-32-characters",
        }),
        TimeProvider.System);

    public ValueTask InitializeAsync() => cosmos.InitializeAsync();

    public ValueTask DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task Post_AddsTheRites_AndAnswersTheWholeList_LowestFirst()
    {
        await Send("POST", """{"rites":[3,1]}""", SignedIn("583231"));

        var (status, body) = await Send("POST", """{"rites":[2,3]}""", SignedIn("583231"));

        Assert.Equal((StatusCodes.Status200OK, """{"rites":[1,2,3]}"""), (status, body));
    }

    [Fact]
    public async Task Get_GivesTheList_FromAnyBrowserSignedInToTheSameAccount()
    {
        await Send("POST", """{"rites":[5]}""", SignedIn("583231"));

        Assert.Equal((StatusCodes.Status200OK, """{"rites":[5]}"""), await Send("GET", string.Empty, SignedIn("583231")));
        Assert.Equal((StatusCodes.Status200OK, """{"rites":[]}"""), await Send("GET", string.Empty, SignedIn("another-account")));
    }

    [Fact]
    public async Task Delete_ForgetsTheList_ForEveryBrowser()
    {
        await Send("POST", """{"rites":[5,6]}""", SignedIn("583231"));

        var (status, _) = await Send("DELETE", string.Empty, SignedIn("583231"));

        Assert.Equal(StatusCodes.Status204NoContent, status);
        Assert.Equal((StatusCodes.Status200OK, """{"rites":[]}"""), await Send("GET", string.Empty, SignedIn("583231")));
        Assert.Equal(StatusCodes.Status204NoContent, (await Send("DELETE", string.Empty, SignedIn("583231"))).Status);
    }

    [Fact]
    public async Task Post_FromTwoTabsAtOnce_KeepsBothRites()
    {
        for (var round = 0; round < 5; round++)
        {
            var answers = await Task.WhenAll(
                Repository.Add(new VoterKey("reader-key"), [2 * round + 1], CancellationToken.None),
                Repository.Add(new VoterKey("reader-key"), [2 * round + 2], CancellationToken.None));

            Assert.All(answers, answer => Ok(answer));
        }

        Assert.Equal(Enumerable.Range(1, 10), Ok(await Repository.Get(new VoterKey("reader-key"), CancellationToken.None)));
    }

    // Only an abuser gets this far: the lowest numbers make room, so the document stays small.
    [Fact]
    public async Task Add_PastTheCap_KeepsTheHighestNumbers()
    {
        Ok(await Repository.Add(new VoterKey("reader-key"), Enumerable.Range(1, WitnessedRepository.MaxRites).ToList(), CancellationToken.None));

        var kept = Ok(await Repository.Add(new VoterKey("reader-key"), [WitnessedRepository.MaxRites + 1], CancellationToken.None));

        Assert.Equal((WitnessedRepository.MaxRites, 2, WitnessedRepository.MaxRites + 1), (kept.Count, kept[0], kept[^1]));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task WithoutASignIn_IsUnauthorized(string method)
    {
        var (status, _) = await Send(method, """{"rites":[1]}""", string.Empty);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Empty(Ok(await Repository.Get(session.VoterKeyFor("github", "583231"), CancellationToken.None)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"rites":[]}""")]
    [InlineData("""{"rites":[0]}""")]
    [InlineData("""{"rites":[1,-2]}""")]
    [InlineData("""{"rites":"1"}""")]
    [InlineData("""{"rites":[1],"unexpected":true}""")]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task Post_WithABodyThatIsNotRites_IsABadRequest(string json)
        => Assert.Equal(StatusCodes.Status400BadRequest, (await Send("POST", json, SignedIn("583231"))).Status);

    [Fact]
    public async Task Post_WithMoreRitesThanARequestHolds_IsABadRequest()
    {
        var json = JsonSerializer.Serialize(new { rites = Enumerable.Range(1, WitnessedFunction.MaxRitesPerRequest + 1) });

        Assert.Equal(StatusCodes.Status400BadRequest, (await Send("POST", json, SignedIn("583231"))).Status);
    }

    [Fact]
    public async Task Post_WithoutJson_IsRefused_SoAnotherSitesFormCantAdd()
        => Assert.Equal(StatusCodes.Status415UnsupportedMediaType,
            (await Send("POST", "rites=1", SignedIn("583231"), "application/x-www-form-urlencoded")).Status);

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task WhenTheListCantBeReached_Is503(string method)
        => Assert.Equal(StatusCodes.Status503ServiceUnavailable, (await Send(MissingData(), method, """{"rites":[1]}""", SignedIn("583231"), "application/json")).Status);

    private WitnessedRepository Repository => new(cosmos.Container);

    private WitnessedFunction Function() => new(Repository, session, NullLogger<WitnessedFunction>.Instance);

    private WitnessedFunction MissingData()
        => new(new WitnessedRepository(cosmos.Container.Database.GetContainer("no-such-container")), session, NullLogger<WitnessedFunction>.Instance);

    private string SignedIn(string accountId) => $"{VoterSession.CookieName}={session.Issue(session.VoterKeyFor("github", accountId))}";

    private Task<(int Status, string Body)> Send(string method, string body, string cookie)
        => Send(Function(), method, body, cookie, "application/json");

    private Task<(int Status, string Body)> Send(string method, string body, string cookie, string contentType)
        => Send(Function(), method, body, cookie, contentType);

    private static async Task<(int Status, string Body)> Send(WitnessedFunction function, string method, string body, string cookie, string contentType)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = method;
        if (cookie.Length > 0)
        {
            request.Headers.Cookie = cookie;
        }
        if (method == "POST")
        {
            request.ContentType = contentType;
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }
        var result = await function.Witnessed(request, CancellationToken.None);
        Assert.Equal("no-store", request.HttpContext.Response.Headers.CacheControl.ToString());
        return result switch
        {
            OkObjectResult ok => (StatusCodes.Status200OK, JsonSerializer.Serialize(ok.Value)),
            IStatusCodeActionResult other => (other.StatusCode ?? 0, string.Empty),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected result {result}."),
        };
    }
}
