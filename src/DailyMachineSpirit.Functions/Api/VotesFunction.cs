using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Telemetry;
using DailyMachineSpirit.Functions.Voters;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// Blessed or Heresy, by readers signed in with GitHub (<see cref="VoterSession"/>): one vote per account per rite,
/// which they can switch or take back. Only the voter key is kept. A JSON body is required, which a page on another
/// site can't send without CORS, which this API doesn't grant; nor would the sign-in cookie go with it (SameSite).
/// </summary>
public sealed class VotesFunction
{
    // A page shows one rite's buttons; a few more leave room without letting one request read hundreds of votes.
    public const int MaxRitesPerRequest = 20;

    private static readonly JsonSerializerOptions BodyOptions = new(JsonSerializerDefaults.Web)
    {
        // A misspelled field ("vot") is a mistake to refuse, not a vote taken back.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly IVoteRepository votes;
    private readonly VoterSession session;
    private readonly PurgeBatcher purges;
    private readonly TimeProvider time;
    private readonly ILogger<VotesFunction> logger;

    public VotesFunction(IVoteRepository votes, VoterSession session, PurgeBatcher purges, TimeProvider time, ILogger<VotesFunction> logger)
    {
        this.votes = votes;
        this.session = session;
        this.purges = purges;
        this.time = time;
        this.logger = logger;
    }

    [Function("Vote")]
    public async Task<IActionResult> Vote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/rites/{number:int}/vote")] HttpRequest request,
        int number,
        CancellationToken cancellationToken)
    {
        NeverCached(request);
        var voter = session.SignedIn(request);
        if (voter.IsNone)
        {
            return new UnauthorizedResult();
        }
        if (!request.HasJsonContentType())
        {
            return new StatusCodeResult(StatusCodes.Status415UnsupportedMediaType);
        }

        VoteRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<VoteRequest>(request.Body, BodyOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return new BadRequestResult();
        }
        if (body is null || !TryRead(body.Vote, out var vote))
        {
            return new BadRequestResult();
        }

        var voterKey = voter.IfNone(string.Empty);
        var saved = await votes.Vote(number, voterKey, vote, cancellationToken);
        // Taken after the save: any page whose counts were read later has this vote (see Script).
        var countedAt = time.GetUtcNow().ToUnixTimeMilliseconds();
        if (saved.Exists(counts => counts.IsSome))
        {
            purges.Add(Paths.ShowingVotesOf(number));
            logger.CountVote(Name(vote) ?? "withdrawn");
        }
        return saved.Match(
            Right: counts => counts.Match<IActionResult>(
                Some: shown => new OkObjectResult(new { blessed = shown.Blessed, heresy = shown.Heresy, vote = Name(vote), countedAt }),
                None: () => new NotFoundResult()),
            Left: error =>
            {
                logger.LogError(error.ToException(), "A vote on rite NO. {Number} couldn't be saved: {Reason}", number, error.Message);
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }

    /// <summary>The signed-in reader's votes on the rites in <c>?rites=12,13</c>, so the page can show them pressed.</summary>
    [Function("Votes")]
    public async Task<IActionResult> Votes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/votes")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        NeverCached(request);
        var voter = session.SignedIn(request);
        if (voter.IsNone)
        {
            return new UnauthorizedResult();
        }
        var rites = RiteNumbers(request.Query["rites"].ToString());
        if (rites.IsNone)
        {
            return new BadRequestResult();
        }

        var voterKey = voter.IfNone(string.Empty);
        return (await votes.GetVotes(voterKey, rites.IfNone([]), cancellationToken)).Match<IActionResult>(
            Right: found => new OkObjectResult(found.ToDictionary(
                vote => vote.Key.ToString(CultureInfo.InvariantCulture), vote => Name(Some(vote.Value)))),
            Left: error =>
            {
                logger.LogError(error.ToException(), "A reader's votes couldn't be read: {Reason}", error.Message);
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }

    // Rite numbers, comma-separated: at least one, and no more than MaxRitesPerRequest.
    private static Option<IReadOnlyCollection<int>> RiteNumbers(string query)
    {
        var parts = query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var numbers = parts
            .Select(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0 ? Some(number) : None)
            .ToList();
        return numbers.Count is > 0 and <= MaxRitesPerRequest && numbers.All(number => number.IsSome)
            ? Some<IReadOnlyCollection<int>>(numbers.Somes().ToList())
            : None;
    }

    // Exactly the names the page sends, lowercase; anything else isn't a vote. Null takes the vote back.
    private static bool TryRead(string? value, out Option<Reaction> vote)
    {
        vote = value switch
        {
            "blessed" => Some(Reaction.Blessed),
            "heresy" => Some(Reaction.Heresy),
            _ => None,
        };
        return value is null || vote.IsSome;
    }

    // Null is the JSON for no vote.
    private static string? Name(Option<Reaction> vote)
        => vote.MatchUnsafe(Some: reaction => reaction == Reaction.Blessed ? "blessed" : "heresy", None: () => null);

    private static void NeverCached(HttpRequest request) => request.HttpContext.Response.Headers.CacheControl = "no-store";
}
