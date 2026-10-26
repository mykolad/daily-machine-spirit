using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using DailyMachineSpirit.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// What the MCP connector exports (#145), caught by in-memory exporters in place of OTLP. The SDK's activity source is
/// process-wide, so another test's tool calls would be caught too: these run apart from other tests.
/// </summary>
[Collection(nameof(ConnectorTelemetryTests))]
[CollectionDefinition(nameof(ConnectorTelemetryTests), DisableParallelization = true)]
public sealed class ConnectorTelemetryTests : IAsyncLifetime
{
    private const string CallersUserAgent = "CallersOwnAgent/9.9";
    private const string CallersAddress = "203.0.113.77";
    private const string ListRites = """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_rites","arguments":{}}}""";
    private const string RevealTruthOf999 = """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"reveal_heretical_truth","arguments":{"number":999}}}""";

    private readonly FakeRitesApi api = new();
    private readonly List<Activity> spans = [];
    private readonly List<LogRecord> logs = [];
    private WebApplication app = WebApplication.Create();
    private HttpClient http = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration[$"{RitesApiOptions.SectionName}:{nameof(RitesApiOptions.BaseUrl)}"] = FakeRitesApi.BaseUrl;
        builder.Services.AddConnector(builder.Configuration);
        builder.Services.AddConnectorTelemetry(builder.Configuration)
            .WithTracing(tracing => tracing.AddInMemoryExporter(spans))
            .WithLogging(logging => logging.AddInMemoryExporter(logs));
        builder.Services.AddHttpClient<RitesApi>().ConfigurePrimaryHttpMessageHandler(() => api);
        app = builder.Build();
        app.UseConnectorTelemetry();
        app.MapConnector();
        await app.StartAsync();
        http = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        http.Dispose();
        await app.DisposeAsync();
    }

    // The trace starts at the tool call, so none is dropped with the request's unrecorded activity, and a refusal is
    // marked as the SDK marks it, which the dashboard counts as an error.
    [Theory]
    [InlineData(ListRites, RiteTools.ListRitesName, null)]
    [InlineData(RevealTruthOf999, RiteTools.RevealHereticalTruthName, "tool_error")]
    public async Task AToolCall_IsTraced(string call, string tool, string? errorType)
    {
        await Call(call);

        var span = Assert.Single(spans, span => span.Source.Name == ConnectorTelemetry.McpSource);
        Assert.Equal((tool, errorType, default), ((string?)span.GetTagItem("gen_ai.tool.name"), (string?)span.GetTagItem("error.type"), span.ParentSpanId));
    }

    [Fact]
    public async Task NothingAboutTheCaller_IsExported()
    {
        await Call(ListRites);
        await Call(RevealTruthOf999);

        var exported = spans.SelectMany(span => span.TagObjects.Select(tag => tag.Value?.ToString()))
            .Concat(logs.SelectMany(log => (log.Attributes ?? []).Select(attribute => attribute.Value?.ToString())))
            .Concat(logs.Select(log => log.FormattedMessage));
        Assert.DoesNotContain(exported, value => value is not null && (value.Contains(CallersUserAgent) || value.Contains(CallersAddress)));
        Assert.DoesNotContain(spans, span => span.Source.Name == "Microsoft.AspNetCore");
    }

    private async Task Call(string body)
    {
        var before = ToolCallSpans();
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost" + Connector.McpPath)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.UserAgent.ParseAdd(CallersUserAgent);
        request.Headers.Add("X-Forwarded-For", CallersAddress);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        // The SDK's span ends after the answer is written, so the test waits for it rather than for the answer alone.
        await response.Content.ReadAsStringAsync();
        await Eventually(() => ToolCallSpans() > before);
    }

    private int ToolCallSpans() => spans.Count(span => span.Source.Name == ConnectorTelemetry.McpSource);

    private static async Task Eventually(Func<bool> condition)
    {
        for (var tries = 0; !condition() && tries < 100; tries++)
        {
            await Task.Delay(20);
        }
    }
}
