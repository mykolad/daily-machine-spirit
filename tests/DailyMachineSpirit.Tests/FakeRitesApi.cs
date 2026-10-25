using System.Net;

namespace DailyMachineSpirit.Tests;

/// <summary>The public rites API (docs/api.md) as the connector reads it: an answer per path, else 404. Records the paths asked.</summary>
public sealed class FakeRitesApi : HttpMessageHandler
{
    public const string BaseUrl = "https://dailymachinespirit.test/";

    public const string Rites = """{"rites":[{"number":12,"day":"2026-10-14","kind":"ritual","title":"The Rite of Re-Run","text":"Press Re-run thrice.","ailment":"A test that passes on a rerun.","subjects":[{"key":"testing","label":"Testing"}],"url":"https://dailymachinespirit.fyi/r/12"}]}""";

    public const string TruthOf12 = """{"number":12,"hereticalTruth":"A test that passes on a rerun depends on timing or order."}""";

    public Dictionary<string, (HttpStatusCode Status, string Body)> Answers { get; } = new()
    {
        ["/api/v1/rites"] = (HttpStatusCode.OK, Rites),
        ["/api/v1/rites/12/heretical-truth"] = (HttpStatusCode.OK, TruthOf12),
    };

    public bool Unreachable { get; set; }

    public List<string> Paths { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        Paths.Add(path);
        if (Unreachable)
        {
            throw new HttpRequestException("No route to the site.");
        }
        var (status, body) = Answers.TryGetValue(path, out var answer) ? answer : (HttpStatusCode.NotFound, """{"error":"not found"}""");
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
