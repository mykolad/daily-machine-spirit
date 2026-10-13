using System.Net;
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

    [Fact]
    public async Task Fonts_AreServed()
        => AssertAnswers(await app.Get("/fonts/cinzel.woff2"), HttpStatusCode.OK, "wOF2");

    private static void AssertAnswers(Page page, HttpStatusCode status, string containing)
    {
        Assert.True(page.Status == status, $"{page.Path} answered {(int)page.Status}, not {(int)status}.");
        Assert.True(page.Text.Contains(containing, StringComparison.Ordinal), $"{page.Path} doesn't contain \"{containing}\".");
    }

    [GeneratedRegex("data-rite=\"(?<number>[0-9]+)\"")]
    private static partial Regex TodaysRite();
}
