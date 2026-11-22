using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The rites API described for the tools that read one (#178: assistants' actions, API directories, client generators):
/// an OpenAPI 3.1 document, written by hand beside the code it describes (<c>Api/openapi.json</c>), as the API is two
/// read-only addresses. Its tests check it against the API's real answers, so it can't drift.
/// </summary>
public sealed class OpenApiFunction
{
    public const string Address = "/api/v1/openapi.json";

    private static readonly string Document = Read();

    [Function("OpenApiV1")]
    public IActionResult OpenApi([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/v1/openapi.json")] HttpRequest request)
    {
        // Open to any site's page, as the API it describes is.
        var headers = request.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.AccessControlAllowOrigin = "*";
        // It changes only with a deployment, which no purge follows: an hour at Cloudflare at most.
        EdgeLifetime.UntilPurged.WriteTo(headers);
        return new ContentResult { Content = Document, ContentType = "application/json; charset=utf-8", StatusCode = StatusCodes.Status200OK };
    }

    private static string Read()
    {
        using var stream = typeof(OpenApiFunction).Assembly.GetManifestResourceStream("DailyMachineSpirit.Functions.Api.openapi.json")
            ?? throw new InvalidOperationException("The OpenAPI document isn't embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
