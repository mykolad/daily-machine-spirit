using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace DailyMachineSpirit.SmokeTests;

[Collection(DeployedAppCollection.Name)]
public sealed partial class DeployedAppTests
{
    private const string SiteName = "The Daily Machine Spirit";
    private const string NoRiteYet = "The Machine Spirit slumbers.";

    private readonly DeployedApp app;
    private readonly ITestOutputHelper output;

    public DeployedAppTests(DeployedApp app, ITestOutputHelper output)
    {
        this.app = app;
        this.output = output;
    }

    [Fact]
    public void Healthz_ReportsHealthyAndTheDeployedVersion()
    {
        Assert.Equal("healthy", app.Health.GetProperty("status").GetString());
        Assert.Equal(app.Version, app.Health.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Today_Answers()
        => AssertAnswers(await app.Get("/"), HttpStatusCode.OK, $"<title>{SiteName}</title>");

    [Fact]
    public async Task Archive_Answers()
        => AssertAnswers(await app.Get("/archive"), HttpStatusCode.OK, $"<title>The Archive · {SiteName}</title>");

    // Its query (rites by a stored tag) is its own, so the deployed database answers it too.
    [Fact]
    public async Task ATagsPage_Answers()
        => AssertAnswers(await app.Get("/tags/builds"), HttpStatusCode.OK, $"<title>Filed under Builds · {SiteName}</title>");

    // A query of its own, over the rites' text, for the deployed database to answer.
    [Fact]
    public async Task Search_Answers()
        => AssertAnswers(await app.Get("/archive?q=machine"), HttpStatusCode.OK, "<title>“machine”: ");

    // What the nav's search field matches as the reader types: every rite's words, from the deployed database.
    [Fact]
    public async Task TheSearchIndex_Answers()
        => AssertAnswers(await app.Get("/api/rites"), HttpStatusCode.OK, "\"number\":");

    // Search was a page of its own: links to it lead to the Archive's search (the client follows the redirect).
    [Fact]
    public async Task TheOldSearchAddress_LeadsToTheArchivesSearch()
        => AssertAnswers(await app.Get("/search?q=machine"), HttpStatusCode.OK, "<title>“machine”: ");

    // Today's rite rather than one from the archive, which leaves today's out.
    [Fact]
    public async Task TodaysRite_HasItsOwnPage()
    {
        var today = (await app.Get("/")).Text;
        var rite = TodaysRite().Match(today);
        if (!rite.Success)
        {
            // A new environment has no rite until its first midnight, and Today says so. Without that message, a missing
            // rite is a broken page, not an empty environment.
            Assert.True(today.Contains(NoRiteYet, StringComparison.Ordinal), $"Today shows neither a rite nor \"{NoRiteYet}\"");
            output.WriteLine("No rite published yet, so there's no rite page to check.");
            return;
        }

        AssertAnswers(await app.Get($"/r/{rite.Groups["number"].Value}"), HttpStatusCode.OK, $" · {SiteName}</title>");
    }

    [Fact]
    public async Task UnknownAddress_AnswersWithTheNotFoundPage()
        => AssertAnswers(await app.Get("/smoke-test-no-such-page"), HttpStatusCode.NotFound, $"<title>No such rite exists · {SiteName}</title>");

    // An API route reaches its own function, not the catch-all's 404 page (which took /api/votes once): without a
    // sign-in, the reader's votes are refused.
    [Fact]
    public async Task TheReadersVotes_AreAskedOfTheVotesApi()
    {
        var page = await app.Get("/api/votes?rites=1");

        Assert.True(page.Status == HttpStatusCode.Unauthorized, $"{page.Path} answered {(int)page.Status}, not 401.");
    }

    // The whole way a signed-in reader votes, past the routing and the sign-in that unit tests skip: the vote counts,
    // the page can load it, and taking it back leaves the counts as they were. As the test voter (staging only).
    [TestVoterFact]
    public async Task ASignedInReader_VotesAndTakesTheVoteBack()
    {
        var rite = TodaysRite().Match((await app.Get("/")).Text);
        if (!rite.Success)
        {
            output.WriteLine("No rite published yet, so there's nothing to vote on.");
            return;
        }
        var number = rite.Groups["number"].Value;
        var cookie = TestVoter.Cookie();

        // A run stopped halfway may have left the test voter's vote: taking it back first starts from none.
        var before = await Vote(number, cookie, null);
        JsonElement blessed;
        JsonElement loaded;
        JsonElement taken;
        // Taken back even when voting or loading fails: the vote may be saved all the same, and a later run takes back
        // only the newest rite's.
        try
        {
            blessed = await Vote(number, cookie, "blessed");
            loaded = await ReadersVotes(number, cookie);
        }
        finally
        {
            taken = await Vote(number, cookie, null);
        }

        Assert.Equal("blessed", blessed.GetProperty("vote").GetString());
        Assert.Equal(before.GetProperty("blessed").GetInt32() + 1, blessed.GetProperty("blessed").GetInt32());
        Assert.Equal(before.GetProperty("heresy").GetInt32(), blessed.GetProperty("heresy").GetInt32());
        Assert.Equal("blessed", loaded.GetProperty(number).GetString());
        Assert.Equal(JsonValueKind.Null, taken.GetProperty("vote").ValueKind);
        Assert.Equal(before.GetProperty("blessed").GetInt32(), taken.GetProperty("blessed").GetInt32());
    }

    [Fact]
    public async Task Fonts_AreServed()
        => AssertAnswers(await app.Get("/fonts/cinzel.woff2"), HttpStatusCode.OK, "wOF2");

    // As the page's script votes: JSON, with the sign-in cookie. The answer has the counts and the vote now.
    private async Task<JsonElement> Vote(string number, string cookie, string? vote)
    {
        var page = await app.Send(() => new HttpRequestMessage(HttpMethod.Post, $"/api/rites/{number}/vote")
        {
            Headers = { { "Cookie", $"{TestVoter.CookieName}={cookie}" } },
            Content = JsonContent.Create(new { vote }),
        });
        Assert.True(page.Status == HttpStatusCode.OK,
            $"Voting {vote ?? "nothing"} answered {(int)page.Status}: {page.Text}");
        return JsonSerializer.Deserialize<JsonElement>(page.Text);
    }

    // What the page asks to show the reader's own vote pressed.
    private async Task<JsonElement> ReadersVotes(string number, string cookie)
    {
        var page = await app.Send(() => new HttpRequestMessage(HttpMethod.Get, $"/api/votes?rites={number}")
        {
            Headers = { { "Cookie", $"{TestVoter.CookieName}={cookie}" } },
        });
        Assert.True(page.Status == HttpStatusCode.OK, $"{page.Path} answered {(int)page.Status}: {page.Text}");
        return JsonSerializer.Deserialize<JsonElement>(page.Text);
    }

    private static void AssertAnswers(Page page, HttpStatusCode status, string containing)
    {
        Assert.True(page.Status == status, $"{page.Path} answered {(int)page.Status}, not {(int)status}.");
        Assert.True(page.Text.Contains(containing, StringComparison.Ordinal), $"{page.Path} doesn't contain \"{containing}\".");
    }

    [GeneratedRegex("data-rite=\"(?<number>[0-9]+)\"")]
    private static partial Regex TodaysRite();
}
