using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Functions.Caching;

/// <summary>
/// Purges pages from Cloudflare's cache by URL, with an API token that may only do that. Without the settings (staging,
/// a local run: nothing caches their pages) it does nothing. A purge that fails is logged and counted, and the page
/// stays at most as long as its edge lifetime.
/// </summary>
public sealed class CloudflarePageCache : IPageCache
{
    public const string HttpClientName = "cloudflare-api";

    // The most URLs one purge-by-URL request may name on Cloudflare's Free plan: a longer list is refused whole.
    public const int MaxUrlsPerPurge = 30;

    private readonly IHttpClientFactory httpClients;
    private readonly IOptions<CloudflareOptions> options;
    private readonly SiteMetrics metrics;
    private readonly ILogger<CloudflarePageCache> logger;

    public CloudflarePageCache(IHttpClientFactory httpClients, IOptions<CloudflareOptions> options, SiteMetrics metrics, ILogger<CloudflarePageCache> logger)
    {
        this.httpClients = httpClients;
        this.options = options;
        this.metrics = metrics;
        this.logger = logger;
    }

    public async Task Purge(IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        var cloudflare = options.Value;
        if (!cloudflare.IsConfigured)
        {
            return;
        }

        foreach (var batch in paths.Chunk(MaxUrlsPerPurge))
        {
            await PurgeBatch(cloudflare, batch, cancellationToken);
        }
    }

    private async Task PurgeBatch(CloudflareOptions cloudflare, IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        try
        {
            var site = cloudflare.SiteUrl.TrimEnd('/');
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.cloudflare.com/client/v4/zones/{cloudflare.ZoneId}/purge_cache")
            {
                Content = JsonContent.Create(new { files = paths.Select(path => site + path).ToList() }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cloudflare.ApiToken);
            using var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            var answer = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var succeeded = response.IsSuccessStatusCode
                && answer.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True;
            if (succeeded)
            {
                metrics.PagePurge("purged");
            }
            else
            {
                Failed(Error.New($"Cloudflare answered {(int)response.StatusCode}: {answer}"), paths);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Failed(Error.New(ex), paths);
        }
    }

    private void Failed(Error error, IReadOnlyCollection<string> paths)
    {
        logger.LogWarning(error.ToException(),
            "Cloudflare didn't purge {Paths}; they refresh within their edge lifetime: {Reason}", string.Join(", ", paths), error.Message);
        metrics.PagePurge("failed");
    }
}
