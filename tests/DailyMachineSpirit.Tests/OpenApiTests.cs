using System.Reflection;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// The rites API's OpenAPI document (#178), written by hand, checked against the API itself: its addresses, the fields
/// of its real answers, and the statuses and content types it answers with, so the document can't drift.
/// </summary>
public sealed class OpenApiTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();

    public ValueTask InitializeAsync() => cosmos.InitializeAsync();

    public ValueTask DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public void TheDocument_IsServed_OpenToAll_AndKeptAtTheEdge()
    {
        var context = At(OpenApiFunction.Address);

        var result = Assert.IsType<ContentResult>(new OpenApiFunction().OpenApi(context.Request));

        Assert.Equal((StatusCodes.Status200OK, "application/json; charset=utf-8"), (result.StatusCode, result.ContentType));
        Assert.Equal("3.1.0", JsonDocument.Parse(result.Content!).RootElement.GetProperty("openapi").GetString());
        Assert.Equal(("*", "public, max-age=3600, stale-if-error=604800"),
            (context.Response.Headers.AccessControlAllowOrigin.ToString(), context.Response.Headers["Cloudflare-CDN-Cache-Control"].ToString()));
    }

    // The document's paths are the API's routes ({id} is read as text, the document's {number}).
    [Fact]
    public void ItsPaths_AreTheApisRoutes()
    {
        var routes = typeof(RitesFunction).GetMethods()
            .Where(method => method.GetCustomAttribute<FunctionAttribute>() is not null)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.GetCustomAttribute<HttpTriggerAttribute>())
            .OfType<HttpTriggerAttribute>()
            .Select(trigger => "/" + trigger.Route!.Replace("{id}", "{number}", StringComparison.Ordinal))
            .Order();

        Assert.Equal(routes, Paths().Select(path => path.Name).Order());
    }

    // Every field of every real answer is in its schema, and required, and the schema names no other.
    [Fact]
    public async Task ItsSchemas_NameEveryFieldOfTheRealAnswers()
    {
        await AddRite();
        var function = Function(Repository);

        var rites = Json(await function.Rites(At("/api/v1/rites").Request, CancellationToken.None));
        var truth = Json(await function.HereticalTruth(At("/api/v1/rites/1/heretical-truth").Request, "1", CancellationToken.None));
        var noRite = Json(await function.HereticalTruth(At("/api/v1/rites/9/heretical-truth").Request, "9", CancellationToken.None));
        var unavailable = Json(await Function(Missing).Rites(At("/api/v1/rites").Request, CancellationToken.None));

        var rite = rites.GetProperty("rites")[0];
        foreach (var (schema, answer) in new[]
        {
            ("PublishedRites", rites), ("PublishedRite", rite), ("RiteSubject", rite.GetProperty("subjects")[0]),
            ("RevealedTruth", truth), ("Problem", noRite), ("Problem", unavailable),
        })
        {
            var fields = answer.EnumerateObject().Select(field => field.Name).Order().ToList();
            Assert.Equal(fields, Schema(schema).GetProperty("properties").EnumerateObject().Select(field => field.Name).Order());
            Assert.Equal(fields, Schema(schema).GetProperty("required").EnumerateArray().Select(field => field.GetString()).Order());
        }
    }

    // Each address answers as its document says: the statuses it lists, each with the content type listed for it.
    [Fact]
    public async Task ItsStatuses_AreTheOnesTheApiAnswers()
    {
        await AddRite();
        var function = Function(Repository);
        var withQuery = At("/api/v1/rites");
        withQuery.Request.QueryString = new QueryString("?fresh=1");
        var answers = new Dictionary<string, IActionResult[]>
        {
            ["/api/v1/rites"] =
            [
                await function.Rites(At("/api/v1/rites").Request, CancellationToken.None),
                await function.Rites(withQuery.Request, CancellationToken.None),
                await function.Rites(At("/API/v1/rites").Request, CancellationToken.None),
                await Function(Missing).Rites(At("/api/v1/rites").Request, CancellationToken.None),
            ],
            ["/api/v1/rites/{number}/heretical-truth"] =
            [
                await function.HereticalTruth(At("/api/v1/rites/1/heretical-truth").Request, "1", CancellationToken.None),
                await function.HereticalTruth(At("/api/v1/rites/1/heretical-truth/").Request, "1", CancellationToken.None),
                await function.HereticalTruth(At("/api/v1/rites/9/heretical-truth").Request, "9", CancellationToken.None),
                await Function(Missing).HereticalTruth(At("/api/v1/rites/1/heretical-truth").Request, "1", CancellationToken.None),
            ],
        };

        foreach (var path in Paths())
        {
            var listed = path.Value.GetProperty("get").GetProperty("responses").EnumerateObject()
                .ToDictionary(response => int.Parse(response.Name, System.Globalization.CultureInfo.InvariantCulture), response => ContentTypeOf(response.Value));
            var answered = answers[path.Name].ToDictionary(Status, ContentTypeOf);
            Assert.Equal(listed.OrderBy(entry => entry.Key), answered.OrderBy(entry => entry.Key));
        }
    }

    private static JsonElement Document()
        => JsonDocument.Parse(Assert.IsType<ContentResult>(new OpenApiFunction().OpenApi(At(OpenApiFunction.Address).Request)).Content!).RootElement;

    private static IEnumerable<JsonProperty> Paths() => Document().GetProperty("paths").EnumerateObject();

    private static JsonElement Schema(string name) => Document().GetProperty("components").GetProperty("schemas").GetProperty(name);

    // The content type a listed response names (followed through its $ref), or none for a redirect.
    private static string ContentTypeOf(JsonElement response)
    {
        var resolved = response.TryGetProperty("$ref", out var reference)
            ? Document().GetProperty("components").GetProperty("responses").GetProperty(reference.GetString()!.Split('/')[^1])
            : response;
        return resolved.TryGetProperty("content", out var content) ? content.EnumerateObject().Single().Name : string.Empty;
    }

    private static string ContentTypeOf(IActionResult result)
        => result is ContentResult content ? content.ContentType!.Split(';')[0] : string.Empty;

    private static int Status(IActionResult result) => result switch
    {
        ContentResult content => content.StatusCode!.Value,
        RedirectResult redirect => redirect.Permanent ? StatusCodes.Status301MovedPermanently : StatusCodes.Status302Found,
        _ => throw new Xunit.Sdk.XunitException($"Unexpected answer: {result.GetType().Name}"),
    };

    private static JsonElement Json(IActionResult result) => JsonDocument.Parse(Assert.IsType<ContentResult>(result).Content!).RootElement;

    private static DefaultHttpContext At(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }

    private RiteRepository Repository => new(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance);

    private RiteRepository Missing => new(cosmos.Container.Database.GetContainer("no-such-container"), TimeProvider.System, NullLogger<RiteRepository>.Instance);

    private static RitesFunction Function(IRiteRepository rites) => new(rites, NullLogger<RitesFunction>.Instance);

    private async Task AddRite()
        => Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = new DateOnly(2026, 10, 8),
            Writing = new RiteWriting
            {
                Kind = RiteKind.Ritual,
                Title = "The Rite of Re-Run",
                Text = "Press Re-run thrice, intoning `it passed locally`.",
                HereticalTruth = "Your test depends on `timing`.",
                Ailment = "A test that fails, then passes when run again.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
            },
            Tags = new[] { "testing" },
        }, CancellationToken.None));
}
