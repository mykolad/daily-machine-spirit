using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DailyMachineSpirit.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The MCP connector (#119), on a test server over a fake rites API, called as an assistant's client calls it.</summary>
public sealed class ConnectorTests : IAsyncLifetime
{
    private static readonly Uri McpAddress = new("http://localhost" + Connector.McpPath);

    private readonly FakeRitesApi api = new();
    private WebApplication app = WebApplication.Create();
    private HttpClient http = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration[$"{RitesApiOptions.SectionName}:{nameof(RitesApiOptions.BaseUrl)}"] = FakeRitesApi.BaseUrl;
        builder.Services.AddConnector(builder.Configuration);
        builder.Services.AddHttpClient<RitesApi>().ConfigurePrimaryHttpMessageHandler(() => api);
        app = builder.Build();
        app.MapConnector();
        await app.StartAsync();
        http = app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        http.Dispose();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task TheTools_AreTheTwoReads()
    {
        await using var client = await Client();

        var tools = await client.ListToolsAsync();

        Assert.Equal(["list_rites", "reveal_heretical_truth"], tools.Select(tool => tool.Name).Order());
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint));
    }

    // As the API gives them, so the connector keeps the API's contract (docs/api.md), and its truths stay sealed.
    [Fact]
    public async Task ListRites_GivesTheRitesAsTheApiDoes()
    {
        await using var client = await Client();

        var result = await client.CallToolAsync("list_rites");

        Assert.Equal((false, FakeRitesApi.Rites), (result.IsError ?? false, Text(result)));
        Assert.Equal(["/api/v1/rites"], api.Paths);
    }

    [Fact]
    public async Task RevealHereticalTruth_GivesThatRitesAlone()
    {
        await using var client = await Client();

        var result = await client.CallToolAsync("reveal_heretical_truth", new Dictionary<string, object?> { ["number"] = 12 });

        Assert.Equal((false, FakeRitesApi.TruthOf12), (result.IsError ?? false, Text(result)));
        Assert.Equal(["/api/v1/rites/12/heretical-truth"], api.Paths);
    }

    [Fact]
    public async Task ANumberNoRiteHas_IsSaidSo()
    {
        await using var client = await Client();

        var result = await client.CallToolAsync("reveal_heretical_truth", new Dictionary<string, object?> { ["number"] = 999 });

        Assert.Equal((true, "No rite NO. 999 has been published."), (result.IsError ?? false, Text(result)));
    }

    // Said as the tool's answer, so the assistant tells the user rather than make a rite up.
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.OK, true)]
    public async Task TheApiFailing_IsSaidSo(HttpStatusCode status, bool unreachable)
    {
        api.Answers["/api/v1/rites"] = (status, "{}");
        api.Unreachable = unreachable;
        await using var client = await Client();

        var result = await client.CallToolAsync("list_rites");

        Assert.Equal((true, RiteTools.Unavailable), (result.IsError ?? false, Text(result)));
    }

    // Stateless: a request needs nothing an earlier one left behind, so any instance can answer it.
    [Fact]
    public async Task ARequest_StandsAlone()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, McpAddress)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"list_rites","arguments":{}}}""",
                Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Mcp-Session-Id"));
        Assert.Contains("The Rite of Re-Run", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_SaysWhichCommitItRuns()
    {
        var health = JsonSerializer.Deserialize<JsonElement>(await http.GetStringAsync("/healthz"));

        Assert.Equal(("healthy", ConnectorVersion.Short), (health.GetProperty("status").GetString(), health.GetProperty("version").GetString()));
    }

    [Theory]
    [InlineData("1.0.0+3f9c2e1aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "3f9c2e1")]
    [InlineData("1.0.0+", ConnectorVersion.Local)]
    [InlineData("1.0.0", ConnectorVersion.Local)]
    public void TheVersion_IsTheShortCommit(string informationalVersion, string expected)
        => Assert.Equal(expected, ConnectorVersion.FromInformationalVersion(informationalVersion));

    [Fact]
    public void WithoutAVersion_ItsLocal() => Assert.Equal(ConnectorVersion.Local, ConnectorVersion.FromInformationalVersion(None));

    private async Task<McpClient> Client()
        => await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions { Endpoint = McpAddress, TransportMode = HttpTransportMode.StreamableHttp },
                http, NullLoggerFactory.Instance, false),
            new McpClientOptions(), NullLoggerFactory.Instance, CancellationToken.None);

    private static string Text(CallToolResult result) => Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
