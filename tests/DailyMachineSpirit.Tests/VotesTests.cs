using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Api;
using DailyMachineSpirit.Functions.Voters;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public sealed class VotesTests : IAsyncLifetime
{
    private const string Reader = "reader-key";

    private readonly CosmosTestContainer cosmos = new();
    private readonly VoterSession session = new(
        Options.Create(new VoterOptions
        {
            KeySecret = "key-secret-of-at-least-32-characters",
            SessionSecret = "session-secret-of-at-least-32-characters",
        }),
        TimeProvider.System);

    public Task InitializeAsync() => cosmos.InitializeAsync();

    public Task DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task Vote_CountsANewVote()
    {
        await AddRite();

        Assert.Equal(new ReactionCounts(1, 0), await Vote(Reader, Some(Reaction.Blessed)));
    }

    [Fact]
    public async Task Vote_Switched_MovesTheCount()
    {
        await AddRite();
        await Vote(Reader, Some(Reaction.Blessed));

        Assert.Equal(new ReactionCounts(0, 1), await Vote(Reader, Some(Reaction.Heresy)));
    }

    [Fact]
    public async Task Vote_TakenBack_IsNoLongerCounted()
    {
        await AddRite();
        await Vote(Reader, Some(Reaction.Heresy));

        Assert.Equal(new ReactionCounts(0, 0), await Vote(Reader, None));
        Assert.Empty(Ok(await Repository.GetVotes(Reader, [1], CancellationToken.None)));
    }

    [Fact]
    public async Task Vote_CountsOncePerAccount_HoweverOftenItsSent()
    {
        await AddRite();
        await Vote(Reader, Some(Reaction.Blessed));
        await Vote(Reader, Some(Reaction.Blessed));

        Assert.Equal(new ReactionCounts(1, 0), await Vote(Reader, Some(Reaction.Blessed)));
    }

    [Fact]
    public async Task Vote_ByManyReadersAtOnce_CountsEach()
    {
        await AddRite();

        await Task.WhenAll(Enumerable.Range(0, 12).Select(reader => Vote($"reader-{reader}", Some(Reaction.Blessed))));

        Assert.Equal(new ReactionCounts(12, 0), await Vote("one-more", None));
    }

    [Fact]
    public async Task Vote_FromTwoTabsAtOnce_StillCountsTheReaderOnce()
    {
        await AddRite();

        for (var round = 0; round < 5; round++)
        {
            var answers = await Task.WhenAll(Vote(Reader, Some(Reaction.Blessed)), Vote(Reader, Some(Reaction.Heresy)));

            // Each answer is one consistent state: the reader's vote counted once, on the side it asked for.
            Assert.Equal([new ReactionCounts(1, 0), new ReactionCounts(0, 1)], answers);
            var counts = await Vote("someone-else", None);
            var stored = Ok(await Repository.GetVotes(Reader, [1], CancellationToken.None))[1];
            Assert.Equal(stored == Reaction.Blessed ? new ReactionCounts(1, 0) : new ReactionCounts(0, 1), counts);
        }
    }

    [Fact]
    public async Task Vote_OnARiteThatDoesNotExist_IsNone()
        => Assert.True(Ok(await Repository.Vote(7, Reader, Some(Reaction.Blessed), CancellationToken.None)).IsNone);

    // Rites from before sign-in carry the anonymous counts under other names: they're left behind, and the votes start
    // from zero.
    [Fact]
    public async Task Vote_OnARiteFromBeforeSignIn_StartsFromZero()
    {
        await cosmos.Container.CreateItemAsync(new JsonObject
        {
            ["id"] = "2026-10-01",
            ["partition"] = "rites",
            ["type"] = "rite",
            ["number"] = 1,
            ["publishedOnUtc"] = "2026-10-01",
            ["kind"] = "prayer",
            ["title"] = "Old rite",
            ["text"] = "Text.",
            ["hereticalTruth"] = "Truth.",
            ["generatedByModel"] = "gpt-6-sol",
            ["generatedAtUtc"] = "2026-09-30T00:00:00Z",
            ["blessedCount"] = 7,
            ["heresyCount"] = 2,
        }, new PartitionKey("rites"));

        var rite = Assert.Single(Ok(await new RiteRepository(cosmos.Container, NullLogger<RiteRepository>.Instance)
            .GetByNumbers([1], CancellationToken.None)));
        Assert.Equal((0, 0), (rite.BlessedCount, rite.HeresyCount));
        Assert.Equal(new ReactionCounts(1, 0), await Vote(Reader, Some(Reaction.Blessed)));
    }

    [Fact]
    public async Task GetVotes_GivesOnlyThisReadersVotes_OnTheRitesAsked()
    {
        await AddRite();
        await AddRite(new DateOnly(2026, 10, 10));
        await AddRite(new DateOnly(2026, 10, 11));
        await Vote(Reader, Some(Reaction.Blessed));
        Ok(await Repository.Vote(2, Reader, Some(Reaction.Heresy), CancellationToken.None));
        Ok(await Repository.Vote(3, Reader, Some(Reaction.Heresy), CancellationToken.None));
        Ok(await Repository.Vote(2, "someone-else", Some(Reaction.Blessed), CancellationToken.None));

        var votes = Ok(await Repository.GetVotes(Reader, [1, 2, 9], CancellationToken.None));

        Assert.Equal(new Dictionary<int, Reaction> { [1] = Reaction.Blessed, [2] = Reaction.Heresy }, votes);
    }

    [Fact]
    public async Task WhenTheDataCantBeReached_EveryCallIsAnError()
    {
        var missing = new VoteRepository(cosmos.Container.Database.GetContainer("no-such-container"));

        Assert.True((await missing.Vote(1, Reader, Some(Reaction.Blessed), CancellationToken.None)).IsLeft);
        Assert.True((await missing.GetVotes(Reader, [1], CancellationToken.None)).IsLeft);
    }

    [Fact]
    public async Task Post_SavesTheReadersVote_AndAnswersTheCounts()
    {
        await AddRite();

        var (status, body) = await Post(Function(), 1, """{"vote":"blessed"}""", "application/json", SignedIn());

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("""{"blessed":1,"heresy":0,"vote":"blessed"}""", body);
    }

    [Fact]
    public async Task Post_WithNull_TakesTheVoteBack()
    {
        await AddRite();
        await Post(Function(), 1, """{"vote":"heresy"}""", "application/json", SignedIn());

        var (_, body) = await Post(Function(), 1, """{"vote":null}""", "application/json", SignedIn());

        Assert.Equal("""{"blessed":0,"heresy":0,"vote":null}""", body);
    }

    [Theory]
    [InlineData("", "application/json")]
    [InlineData("not-a-cookie", "application/json")]
    [InlineData("", "application/x-www-form-urlencoded")]
    public async Task Post_WithoutASignIn_IsUnauthorized_AndCountsNothing(string cookie, string contentType)
    {
        await AddRite();

        var (status, _) = await Post(Function(), 1, """{"vote":"blessed"}""", contentType, cookie);

        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Equal(new ReactionCounts(0, 0), await Vote("someone-else", None));
    }

    [Theory]
    [InlineData("""{"vote":"amen"}""")]
    [InlineData("""{"vote":"Blessed"}""")]
    [InlineData("""{"vote":"blessed","unexpected":true}""")]
    [InlineData("""{"vot":"blessed"}""")]
    [InlineData("""{"vote":1}""")]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("null")]
    public async Task Post_WithABodyThatIsNotAVote_IsABadRequest(string json)
    {
        await AddRite();

        var (status, _) = await Post(Function(), 1, json, "application/json", SignedIn());

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(new ReactionCounts(0, 0), await Vote("someone-else", None));
    }

    [Fact]
    public async Task Post_WithoutJson_IsRefused_SoAnotherSitesFormCantVote()
    {
        await AddRite();

        var (status, _) = await Post(Function(), 1, "vote=blessed", "application/x-www-form-urlencoded", SignedIn());

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, status);
    }

    [Fact]
    public async Task Post_ToARiteThatDoesNotExist_IsNotFound()
    {
        var (status, _) = await Post(Function(), 9, """{"vote":"heresy"}""", "application/json", SignedIn());

        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task Post_WhenTheVoteCantBeSaved_Is503()
    {
        var (status, _) = await Post(MissingData(), 1, """{"vote":"heresy"}""", "application/json", SignedIn());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    [Fact]
    public async Task Get_GivesTheReadersVotes_ByRite()
    {
        await AddRite();
        await Post(Function(), 1, """{"vote":"heresy"}""", "application/json", SignedIn());

        var (status, body) = await Get(Function(), "?rites=1,2", SignedIn());

        Assert.Equal((StatusCodes.Status200OK, """{"1":"heresy"}"""), (status, body));
    }

    [Fact]
    public async Task Get_WithoutASignIn_IsUnauthorized()
        => Assert.Equal(StatusCodes.Status401Unauthorized, (await Get(Function(), "?rites=1", string.Empty)).Status);

    [Theory]
    [InlineData("")]
    [InlineData("?rites=")]
    [InlineData("?rites=one")]
    [InlineData("?rites=0")]
    [InlineData("?rites=1,-2")]
    [InlineData("?rites=1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21")]
    public async Task Get_WithoutRiteNumbers_IsABadRequest(string query)
        => Assert.Equal(StatusCodes.Status400BadRequest, (await Get(Function(), query, SignedIn())).Status);

    [Fact]
    public async Task Get_WhenTheVotesCantBeRead_Is503()
        => Assert.Equal(StatusCodes.Status503ServiceUnavailable, (await Get(MissingData(), "?rites=1", SignedIn())).Status);

    private VoteRepository Repository => new(cosmos.Container);

    private VotesFunction Function() => new(Repository, session, NullLogger<VotesFunction>.Instance);

    private VotesFunction MissingData()
        => new(new VoteRepository(cosmos.Container.Database.GetContainer("no-such-container")), session, NullLogger<VotesFunction>.Instance);

    private string SignedIn() => $"{VoterSession.CookieName}={session.Issue(session.VoterKeyFor("github", "583231"))}";

    private async Task<ReactionCounts> Vote(string voterKey, Option<Reaction> vote)
        => Ok(await Repository.Vote(1, voterKey, vote, CancellationToken.None))
            .IfNone(() => throw new Xunit.Sdk.XunitException("Expected rite NO. 1."));

    private static async Task<(int Status, string Body)> Post(VotesFunction function, int number, string body, string contentType, string cookie)
    {
        var request = Request("POST", string.Empty, cookie);
        request.ContentType = contentType;
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return Answer(request, await function.Vote(request, number, CancellationToken.None));
    }

    private static async Task<(int Status, string Body)> Get(VotesFunction function, string query, string cookie)
    {
        var request = Request("GET", query, cookie);
        return Answer(request, await function.Votes(request, CancellationToken.None));
    }

    private static HttpRequest Request(string method, string query, string cookie)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = method;
        request.QueryString = new QueryString(query);
        if (cookie.Length > 0)
        {
            request.Headers.Cookie = cookie;
        }
        return request;
    }

    private static (int Status, string Body) Answer(HttpRequest request, IActionResult result)
    {
        Assert.Equal("no-store", request.HttpContext.Response.Headers.CacheControl.ToString());
        return result switch
        {
            OkObjectResult ok => (StatusCodes.Status200OK, JsonSerializer.Serialize(ok.Value)),
            IStatusCodeActionResult other => (other.StatusCode ?? 0, ""),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected result {result}."),
        };
    }

    private async Task AddRite() => await AddRite(new DateOnly(2026, 10, 9));

    private async Task AddRite(DateOnly day)
        => Ok(await new RiteRepository(cosmos.Container, NullLogger<RiteRepository>.Instance).Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting
            {
                Kind = RiteKind.Prayer,
                Title = "Litany of the Clean Cache",
                Text = "Clear the cache, and the cache shall clear thee.",
                HereticalTruth = "A stale cache hides a missing invalidation.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = new DateTime(2026, 10, 8, 23, 0, 0, DateTimeKind.Utc),
            },
        }, CancellationToken.None));
}
