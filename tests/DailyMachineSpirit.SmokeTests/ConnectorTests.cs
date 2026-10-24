using System.Net;
using System.Text;
using System.Text.Json;

namespace DailyMachineSpirit.SmokeTests;

/// <summary>
/// The MCP connector (#119), deployed: its two tools, called as an assistant's client calls them, read the rites through
/// the public API. Its own deployments run these alone (<see cref="Category"/>); the site's run everything else.
/// </summary>
[Collection(DeployedAppCollection.Name)]
[Trait("Category", Category)]
public sealed class ConnectorTests
{
    public const string Category = "Connector";

    private readonly DeployedApp app;

    public ConnectorTests(DeployedApp app)
    {
        this.app = app;
    }

    [Fact]
    public void Healthz_ReportsHealthyAndTheDeployedVersion()
    {
        Assert.Equal("healthy", app.Health.GetProperty("status").GetString());
        Assert.Equal(app.Version, app.Health.GetProperty("version").GetString());
    }

    [Fact]
    public async Task TheTools_AreListed()
    {
        var tools = (await Call("tools/list", "{}")).GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString());

        Assert.Equal(["list_rites", "reveal_heretical_truth"], tools.Order());
    }

    // Each request stands alone (no initialize, no session), as the connector is stateless.
    [Fact]
    public async Task TheRites_AndARitesTruth_AreRead()
    {
        var rites = JsonSerializer.Deserialize<JsonElement>(ToolText(await Call("tools/call", """{"name":"list_rites","arguments":{}}"""))).GetProperty("rites");

        Assert.Equal(JsonValueKind.Array, rites.ValueKind);
        foreach (var rite in rites.EnumerateArray().Take(1))
        {
            var number = rite.GetProperty("number").GetInt32();
            var truth = await Call("tools/call", $$$"""{"name":"reveal_heretical_truth","arguments":{"number":{{{number}}}}}""");
            Assert.Equal(number, JsonSerializer.Deserialize<JsonElement>(ToolText(truth)).GetProperty("number").GetInt32());
        }
    }

    private async Task<JsonElement> Call(string method, string parameters)
    {
        var page = await app.Send(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent($$"""{"jsonrpc":"2.0","id":1,"method":"{{method}}","params":{{parameters}}}""", Encoding.UTF8, "application/json"),
            };
            request.Headers.Accept.ParseAdd("application/json, text/event-stream");
            return request;
        });
        Assert.True(page.Status == HttpStatusCode.OK, $"{method} answered {(int)page.Status}, not 200: {page.Text}");
        return JsonSerializer.Deserialize<JsonElement>(Message(page.Text)).GetProperty("result");
    }

    // The answer comes as one server-sent event, or as plain JSON: the transport lets the server choose.
    private static string Message(string body)
        => body.Split('\n').Where(line => line.StartsWith("data:", StringComparison.Ordinal)).Select(line => line["data:".Length..].Trim())
            .DefaultIfEmpty(body).First();

    private static string ToolText(JsonElement result)
    {
        Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean(), result.ToString());
        return result.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
    }
}
