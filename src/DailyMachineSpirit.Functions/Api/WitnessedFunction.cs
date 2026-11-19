using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Voters;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The rites a signed-in reader has witnessed (#215), so they follow the reader to every browser they sign in on: read,
/// added to, or forgotten. Only the voter key is kept with them. As for votes, a change needs a JSON body (or, to forget,
/// DELETE), which a page on another site can't send without CORS, which this API doesn't grant; nor would the sign-in
/// cookie go with it (SameSite).
/// </summary>
public sealed class WitnessedFunction
{
    // A browser's first sign-in sends everything it remembered alone: as many as a reader could have witnessed in years,
    // while one request can't fill the whole list.
    public const int MaxRitesPerRequest = 1_000;

    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    private readonly IWitnessedRepository witnessed;
    private readonly VoterSession session;
    private readonly ILogger<WitnessedFunction> logger;

    public WitnessedFunction(IWitnessedRepository witnessed, VoterSession session, ILogger<WitnessedFunction> logger)
    {
        this.witnessed = witnessed;
        this.session = session;
        this.logger = logger;
    }

    [Function("Witnessed")]
    public async Task<IActionResult> Witnessed(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "delete", Route = "api/witnessed")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        if (session.SignedIn(request).Case is not VoterKey voterKey)
        {
            return new UnauthorizedResult();
        }
        if (HttpMethods.IsGet(request.Method))
        {
            return Answer(await witnessed.Get(voterKey, cancellationToken), "read");
        }
        if (HttpMethods.IsDelete(request.Method))
        {
            return (await witnessed.Forget(voterKey, cancellationToken)).Match<IActionResult>(
                Right: _ => new NoContentResult(),
                Left: error => Failed(error, "forgotten"));
        }
        if (!request.HasJsonContentType())
        {
            return new StatusCodeResult(StatusCodes.Status415UnsupportedMediaType);
        }
        var rites = await Read(request, cancellationToken);
        return await rites.MatchAsync(
            Some: async numbers => Answer(await witnessed.Add(voterKey, numbers, cancellationToken), "saved"),
            None: () => new BadRequestResult());
    }

    // At least one rite number, every one above zero, and no more than MaxRitesPerRequest.
    private static async Task<Option<IReadOnlyCollection<int>>> Read(HttpRequest request, CancellationToken cancellationToken)
    {
        WitnessedRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<WitnessedRequest>(request.Body, BodyOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return Option<IReadOnlyCollection<int>>.None;
        }
        return body?.Rites is { Count: > 0 and <= MaxRitesPerRequest } rites && rites.All(number => number > 0)
            ? Option<IReadOnlyCollection<int>>.Some(rites)
            : Option<IReadOnlyCollection<int>>.None;
    }

    private IActionResult Answer(Either<Error, IReadOnlyList<int>> result, string doing)
        => result.Match<IActionResult>(
            Right: rites => new OkObjectResult(new { rites }),
            Left: error => Failed(error, doing));

    private StatusCodeResult Failed(Error error, string doing)
    {
        logger.LogError(error.ToException(), "A reader's witnessed rites couldn't be {Doing}: {Reason}", doing, error.Message);
        return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
    }
}
