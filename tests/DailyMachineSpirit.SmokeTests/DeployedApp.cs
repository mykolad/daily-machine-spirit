using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace DailyMachineSpirit.SmokeTests;

/// <summary>
/// The app at <see cref="BaseUrlVariable"/>, once it runs the commit in <see cref="VersionVariable"/>: until then, no
/// test starts.
/// </summary>
public sealed class DeployedApp : IAsyncLifetime
{
    public const string BaseUrlVariable = "SMOKE_TEST_BASE_URL";
    public const string VersionVariable = "SMOKE_TEST_VERSION";

    // The app restarts after a deploy and may start cold: the new version gets three minutes to answer, by the clock,
    // however long each try takes.
    private static readonly TimeSpan NewVersionDeadline = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(15);
    // An access rule just added for the runner (staging) takes a moment to reach every front end. Until then a request
    // is refused (403) or gets no answer at all, so it's tried again a few times.
    private const int Tries = 4;
    private static readonly TimeSpan BetweenTries = TimeSpan.FromSeconds(5);

    private readonly HttpClient http;

    public DeployedApp()
    {
        http = new HttpClient { BaseAddress = new Uri(Required(BaseUrlVariable)), Timeout = TimeSpan.FromSeconds(30) };
        Version = Required(VersionVariable);
    }

    public string Version { get; }

    public JsonElement Health { get; private set; }

    public async Task InitializeAsync()
    {
        var waited = Stopwatch.StartNew();
        var lastAnswer = "no answer";
        while (waited.Elapsed < NewVersionDeadline)
        {
            try
            {
                using var timeout = new CancellationTokenSource(HealthTimeout);
                var health = JsonSerializer.Deserialize<JsonElement>(await http.GetStringAsync("/healthz", timeout.Token));
                if (health.TryGetProperty("version", out var version) && version.GetString() == Version)
                {
                    Health = health;
                    return;
                }
                lastAnswer = health.ToString();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                lastAnswer = ex.Message;
            }
            await Task.Delay(BetweenTries);
        }
        throw new InvalidOperationException($"/healthz didn't report version {Version} within three minutes: {lastAnswer}");
    }

    public async Task<Page> Get(string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            var isLastTry = attempt == Tries;
            try
            {
                using var response = await http.GetAsync(path);
                if (response.StatusCode != HttpStatusCode.Forbidden || isLastTry)
                {
                    return new Page(path, response.StatusCode, await response.Content.ReadAsByteArrayAsync());
                }
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !isLastTry)
            {
                // No answer at all: tried again like a 403.
            }
            await Task.Delay(BetweenTries);
        }
    }

    public Task DisposeAsync()
    {
        http.Dispose();
        return Task.CompletedTask;
    }

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {variable}: the smoke tests check a deployed app (CLAUDE.md, \"Deploying\").");
}
