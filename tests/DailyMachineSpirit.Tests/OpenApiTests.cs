using System.Reflection;
using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Api;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// The rites API's OpenAPI document (#178), written by hand, checked against the API itself: its addresses, the fields
/// of its real answers, and the statuses and content types it answers with, so the document can't drift.
/// </summary>
public sealed class OpenApiTests : IAsyncLifetime
{
    // The part of JSON Schema the document uses, which the checker below checks (or, for the last two, needn't).
    private static readonly string[] CheckedKeywords =
        ["$ref", "type", "properties", "required", "items", "enum", "const", "minimum", "format", "description", "example"];

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

    // Every real answer with a body matches the schema its operation names for its status, followed through each $ref:
    // the types and values of its fields, nested ones too, and exactly its fields, all required.
    [Fact]
    public async Task EveryRealAnswer_MatchesTheSchemaListedForIt()
    {
        await AddRite();
        var function = Function(Repository);
        var answers = new (string Path, IActionResult Answer)[]
        {
            ("/api/v1/rites", await function.Rites(At("/api/v1/rites").Request, CancellationToken.None)),
            ("/api/v1/rites", await function.Rites(At("/API/v1/rites").Request, CancellationToken.None)),
            ("/api/v1/rites", await Function(Missing).Rites(At("/api/v1/rites").Request, CancellationToken.None)),
            ("/api/v1/rites/{number}/heretical-truth", await function.HereticalTruth(At("/api/v1/rites/1/heretical-truth").Request, "1", CancellationToken.None)),
            ("/api/v1/rites/{number}/heretical-truth", await function.HereticalTruth(At("/api/v1/rites/01/heretical-truth").Request, "01", CancellationToken.None)),
            ("/api/v1/rites/{number}/heretical-truth", await function.HereticalTruth(At("/api/v1/rites/9/heretical-truth").Request, "9", CancellationToken.None)),
            ("/api/v1/rites/{number}/heretical-truth", await Function(Missing).HereticalTruth(At("/api/v1/rites/1/heretical-truth").Request, "1", CancellationToken.None)),
        };

        foreach (var (path, answer) in answers)
        {
            var status = Status(answer).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var listed = MustResolve(Document().GetProperty("paths").GetProperty(path).GetProperty("get").GetProperty("responses").GetProperty(status));
            var schema = listed.GetProperty("content").EnumerateObject().Single().Value.GetProperty("schema");
            Assert.Empty(Mismatches(schema, Json(answer), $"{path} {status}"));
        }
    }

    // The checker itself: it finds a wrong type, a missing or unlisted field, a value outside the schema's, and a $ref
    // to nothing, so a pass above means what it says.
    [Theory]
    [InlineData("""{ "type": "integer" }""", "\"1\"")]
    [InlineData("""{ "type": "object", "required": ["a"], "properties": { "a": { "type": "string" } } }""", "{}")]
    [InlineData("""{ "type": "object", "required": [], "properties": {} }""", """{ "a": 1 }""")]
    [InlineData("""{ "type": "string", "enum": ["prayer"] }""", "\"psalm\"")]
    [InlineData("""{ "type": "integer", "minimum": 1 }""", "0")]
    [InlineData("""{ "type": "string", "format": "date" }""", "\"8 October\"")]
    [InlineData("""{ "type": "string", "format": "uri" }""", "\"/r/1\"")]
    [InlineData("""{ "type": "string", "format": "uri" }""", "\"https:///r/1\"")]
    [InlineData("""{ "type": "array", "items": { "type": "string" } }""", "[1]")]
    [InlineData("""{ "$ref": "#/components/schemas/NoSuchSchema" }""", "{}")]
    [InlineData("""{ "type": "string", "pattern": "^a" }""", "\"a\"")]
    public void TheChecker_FindsWhatDoesntMatch(string schema, string value)
        => Assert.NotEmpty(Mismatches(JsonDocument.Parse(schema).RootElement, JsonDocument.Parse(value).RootElement, "value"));

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

    // The content type a listed response names (followed through its $ref), or none for a redirect.
    private static string ContentTypeOf(JsonElement response)
        => MustResolve(response).TryGetProperty("content", out var content) ? content.EnumerateObject().Single().Name : string.Empty;

    // A response or schema, or the one its $ref names within the document; none if it names nothing there.
    private static Option<JsonElement> Resolved(JsonElement element)
    {
        if (!element.TryGetProperty("$ref", out var reference))
        {
            return Some(element);
        }
        var target = Document();
        foreach (var step in reference.GetString()!.TrimStart('#', '/').Split('/'))
        {
            if (!target.TryGetProperty(step, out target))
            {
                return None;
            }
        }
        return Some(target);
    }

    private static JsonElement MustResolve(JsonElement element)
        => Resolved(element).IfNone(() => throw new Xunit.Sdk.XunitException($"{element} names nothing in the document"));

    // Where the value differs from the schema, for the subset of JSON Schema the document uses. A keyword outside it
    // is a mismatch too, so the document can't use one these tests don't check.
    private static List<string> Mismatches(JsonElement schema, JsonElement value, string at)
        => Resolved(schema).Match(
            Some: resolved => MismatchesOf(resolved, value, at),
            None: () => [$"{at}: {schema} names nothing in the document"]);

    private static List<string> MismatchesOf(JsonElement resolved, JsonElement value, string at)
    {
        var unknown = resolved.EnumerateObject().Select(keyword => keyword.Name).Except(CheckedKeywords).ToList();
        if (unknown.Count > 0)
        {
            return [$"{at}: the checker doesn't know {string.Join(", ", unknown)}"];
        }
        var type = resolved.TryGetProperty("type", out var named) ? named.GetString()! : string.Empty;
        if (!IsOfType(value, type))
        {
            return [$"{at}: {value.ValueKind}, not {type}"];
        }

        var found = new List<string>();
        if (resolved.TryGetProperty("const", out var only) && value.GetRawText() != only.GetRawText())
        {
            found.Add($"{at}: {value.GetRawText()}, not {only.GetRawText()}");
        }
        if (resolved.TryGetProperty("enum", out var allowed) && !allowed.EnumerateArray().Any(option => option.GetRawText() == value.GetRawText()))
        {
            found.Add($"{at}: \"{value}\" isn't one of {allowed}");
        }
        if (resolved.TryGetProperty("minimum", out var minimum) && value.GetInt64() < minimum.GetInt64())
        {
            found.Add($"{at}: {value} is below {minimum}");
        }
        if (resolved.TryGetProperty("format", out var format) && !InFormat(value.GetString()!, format.GetString()!))
        {
            found.Add($"{at}: \"{value}\" isn't a {format}");
        }
        if (resolved.TryGetProperty("items", out var items))
        {
            found.AddRange(value.EnumerateArray().SelectMany((item, index) => Mismatches(items, item, $"{at}[{index}]")));
        }
        if (type == "object")
        {
            var properties = resolved.TryGetProperty("properties", out var listed) ? listed : JsonDocument.Parse("{}").RootElement;
            var required = resolved.TryGetProperty("required", out var names) ? names.EnumerateArray().Select(name => name.GetString()!).ToList() : [];
            var fields = value.EnumerateObject().Select(field => field.Name).ToList();
            var listedNames = properties.EnumerateObject().Select(property => property.Name).ToList();
            found.AddRange(listedNames.Except(required).Select(name => $"{at}.{name}: listed but not required"));
            found.AddRange(required.Except(fields).Select(name => $"{at}.{name}: required but missing"));
            found.AddRange(fields.Except(listedNames).Select(name => $"{at}.{name}: answered but not listed"));
            found.AddRange(value.EnumerateObject()
                .Where(field => properties.TryGetProperty(field.Name, out _))
                .SelectMany(field => Mismatches(properties.GetProperty(field.Name), field.Value, $"{at}.{field.Name}")));
        }
        return found;
    }

    private static bool IsOfType(JsonElement value, string type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        _ => false,
    };

    private static bool InFormat(string value, string format) => format switch
    {
        "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _),
        // A web address with a host: on Linux, .NET reads "/r/1" as an absolute file path.
        "uri" => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && uri.Host.Length > 0,
        _ => false,
    };

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
        // As a real request comes, so the rites' addresses are whole.
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dailymachinespirit.fyi");
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
