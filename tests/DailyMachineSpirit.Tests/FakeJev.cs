using System.Net;
using System.Text.Json.Nodes;
using DailyMachineSpirit.Functions.Generation;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Tests;

/// <summary>Jev's decide API, answering every request with one status and body. Records the requests' bodies and keys.</summary>
public sealed class FakeJev : HttpMessageHandler
{
    public const string ApiKey = "test-key";

    private readonly HttpStatusCode status;
    private readonly string body;

    public FakeJev(HttpStatusCode status, string body)
    {
        this.status = status;
        this.body = body;
    }

    public List<(JsonNode Body, string Authorization)> Requests { get; } = [];

    /// <summary>An answer about builds and mostly repetition, judged "good" or "excellent" half and half.</summary>
    public static FakeJev AnsweringBuildsAndRepetition()
        => new(HttpStatusCode.OK, """
            {
              "answers": {
                "topic": { "probabilities": { "builds": 1.0 } },
                "act": { "probabilities": { "repetition": 0.75, "waiting": 0.25 } },
                "quality": { "probabilities": { "excellent": 0.5, "good": 0.5 } }
              }
            }
            """);

    public JevScorer Scorer(TimeProvider time, string apiKey)
        => new(new HttpClient(this), Options.Create(new JevOptions { ApiKey = apiKey }), time);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = request.Content is { } sent ? await sent.ReadAsStringAsync(cancellationToken) : "{}";
        Requests.Add((JsonNode.Parse(content) ?? new JsonObject(), request.Headers.Authorization?.ToString() ?? string.Empty));
        return new HttpResponseMessage(status) { Content = new StringContent(body) };
    }
}
