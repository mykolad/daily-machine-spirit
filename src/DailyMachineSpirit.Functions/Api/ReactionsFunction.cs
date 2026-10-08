using System.Text.Json;
using System.Text.Json.Serialization;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// Blessed or Heresy on a rite. Anonymous: the browser remembers its own reaction and sends it back as
/// <see cref="ReactionRequest.Previous"/>, so nothing about the visitor is kept. A JSON body is required, which a page on
/// another site can't send without CORS, which this API doesn't grant.
/// </summary>
public sealed class ReactionsFunction
{
    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web)
    {
        // A misspelled field ("reacton") is a mistake to refuse, not a reaction of none.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly IRiteRepository rites;
    private readonly ILogger<ReactionsFunction> logger;

    public ReactionsFunction(IRiteRepository rites, ILogger<ReactionsFunction> logger)
    {
        this.rites = rites;
        this.logger = logger;
    }

    [Function("React")]
    public async Task<IActionResult> React(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/rites/{number:int}/reaction")] HttpRequest request,
        int number,
        CancellationToken cancellationToken)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        if (!request.HasJsonContentType())
            return new StatusCodeResult(StatusCodes.Status415UnsupportedMediaType);

        ReactionRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ReactionRequest>(request.Body, BodyOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestResult();
        }
        if (body is null || !TryRead(body.Reaction, out var reaction) || !TryRead(body.Previous, out var previous))
            return new BadRequestResult();

        var result = await rites.React(number, reaction, previous, cancellationToken);
        return result.Match(
            Right: counts => counts.Match<IActionResult>(
                Some: saved => new OkObjectResult(new { blessed = saved.Blessed, heresy = saved.Heresy }),
                None: () => new NotFoundResult()),
            Left: error =>
            {
                logger.LogError(error.ToException(), "Rite NO. {Number}'s reaction couldn't be saved: {Reason}", number, error.Message);
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }

    // Exactly the names the page sends, lowercase; anything else isn't a reaction. Null is no reaction.
    private static bool TryRead(string? value, out Option<Reaction> reaction)
    {
        reaction = value switch
        {
            "blessed" => Some(Reaction.Blessed),
            "heresy" => Some(Reaction.Heresy),
            _ => None,
        };
        return value is null || reaction.IsSome;
    }
}
