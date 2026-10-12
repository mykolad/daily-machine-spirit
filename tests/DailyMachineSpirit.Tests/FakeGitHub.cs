using System.Net;
using DailyMachineSpirit.Functions.Voters;

namespace DailyMachineSpirit.Tests;

/// <summary>GitHub's token, user and token-revoking endpoints, each answering one way. Records every request.</summary>
public sealed class FakeGitHub : HttpMessageHandler
{
    public const string Token = "gho_test-token";

    public (HttpStatusCode Status, string Body) TokenAnswer { get; set; } = (HttpStatusCode.OK, $$"""{"access_token":"{{Token}}","token_type":"bearer","scope":""}""");

    public (HttpStatusCode Status, string Body) UserAnswer { get; set; } = (HttpStatusCode.OK, """{"id":583231,"login":"octocat"}""");

    public HttpStatusCode RevokeStatus { get; set; } = HttpStatusCode.NoContent;

    public List<(HttpMethod Method, string Url, string Body, HttpRequestMessage Request)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var url = request.RequestUri?.ToString() ?? string.Empty;
        var body = request.Content is { } content ? await content.ReadAsStringAsync(cancellationToken) : string.Empty;
        Requests.Add((request.Method, url, body, request));
        var (status, answer) = url switch
        {
            GitHubAccounts.TokenUrl => TokenAnswer,
            GitHubAccounts.UserUrl => UserAnswer,
            _ when request.Method == HttpMethod.Delete => (RevokeStatus, string.Empty),
            _ => (HttpStatusCode.NotFound, string.Empty),
        };
        return new HttpResponseMessage(status) { Content = new StringContent(answer) };
    }
}
