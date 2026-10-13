using System.Net;
using DailyMachineSpirit.Functions.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Tests;

public sealed class CloudflarePageCacheTests : IDisposable
{
    private readonly FakeCloudflare cloudflare = new();
    private readonly MetricsProbe probe = new();

    public void Dispose() => probe.Dispose();

    [Fact]
    public async Task Purge_AsksCloudflareToDropThePagesFullURLs()
    {
        await Cache(Configured()).Purge(["/", "/r/12"], CancellationToken.None);

        var (method, url, body, request) = Assert.Single(cloudflare.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("https://api.cloudflare.com/client/v4/zones/zone-id/purge_cache", url);
        Assert.Equal("""{"files":["https://example.test/","https://example.test/r/12"]}""", body);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("purge-token", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Purge_CountsWhatCloudflareDid()
    {
        using var purges = probe.Collect<long>("dms.pages.purges");

        await Cache(Configured()).Purge(["/"], CancellationToken.None);

        Assert.Equal(["outcome=purged"], MetricsProbe.Tags(purges, "outcome"));
    }

    // Staging and local runs: nothing caches their pages.
    [Theory]
    [InlineData("", "zone-id", "purge-token")]
    [InlineData("https://example.test", "", "purge-token")]
    [InlineData("https://example.test", "zone-id", "")]
    public async Task Purge_WithoutTheSettings_DoesNothing(string siteUrl, string zoneId, string apiToken)
    {
        await Cache(new CloudflareOptions { SiteUrl = siteUrl, ZoneId = zoneId, ApiToken = apiToken }).Purge(["/"], CancellationToken.None);

        Assert.Empty(cloudflare.Requests);
    }

    [Fact]
    public async Task Purge_OfNoPages_AsksNothing()
    {
        await Cache(Configured()).Purge([], CancellationToken.None);

        Assert.Empty(cloudflare.Requests);
    }

    // The change is saved either way: the page refreshes within its edge lifetime.
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"success":false,"errors":[{"code":10000,"message":"Authentication error"}]}""")]
    [InlineData(HttpStatusCode.OK, """{"success":false,"errors":[]}""")]
    [InlineData(HttpStatusCode.BadGateway, "<html>Bad gateway</html>")]
    public async Task Purge_ThatCloudflareRefuses_DoesntFailTheCaller_AndIsCounted(HttpStatusCode status, string answer)
    {
        cloudflare.Answer = (status, answer);
        using var purges = probe.Collect<long>("dms.pages.purges");

        await Cache(Configured()).Purge(["/"], CancellationToken.None);

        Assert.Equal(["outcome=failed"], MetricsProbe.Tags(purges, "outcome"));
    }

    [Fact]
    public async Task Purge_WhenCloudflareIsUnreachable_DoesntFailTheCaller_AndIsCounted()
    {
        cloudflare.Unreachable = true;
        using var purges = probe.Collect<long>("dms.pages.purges");

        await Cache(Configured()).Purge(["/"], CancellationToken.None);

        Assert.Equal(["outcome=failed"], MetricsProbe.Tags(purges, "outcome"));
    }

    [Fact]
    public async Task Purge_Cancelled_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache(Configured()).Purge(["/"], cancelled.Token));
    }

    private static CloudflareOptions Configured() => new() { SiteUrl = "https://example.test/", ZoneId = "zone-id", ApiToken = "purge-token" };

    private CloudflarePageCache Cache(CloudflareOptions options)
        => new(cloudflare, Options.Create(options), probe.Metrics, NullLogger<CloudflarePageCache>.Instance);
}
