using System.Net;

namespace DailyMachineSpirit.Tests;

/// <summary>Cloudflare's API, answering every request one way (or failing to answer). Records every request.</summary>
public sealed class FakeCloudflare : HttpMessageHandler
{
    public (HttpStatusCode Status, string Body) Answer { get; set; } = (HttpStatusCode.OK, """{"success":true,"errors":[],"messages":[],"result":{"id":"zone"}}""");

    public bool Unreachable { get; set; }

    public List<(HttpMethod Method, string Url, string Body, HttpRequestMessage Request)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken) : string.Empty;
        Requests.Add((request.Method, request.RequestUri?.ToString() ?? string.Empty, body, request));
        if (Unreachable)
        {
            throw new HttpRequestException("No route to Cloudflare.");
        }
        return new HttpResponseMessage(Answer.Status) { Content = new StringContent(Answer.Body) };
    }
}
