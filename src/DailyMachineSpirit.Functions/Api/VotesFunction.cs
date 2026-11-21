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
/// Blessed or Heresy, and a confession to having done the rite (#200), by readers signed in with GitHub
/// (<see cref="VoterSession"/>): one vote per account per rite, whose verdict they can switch or take back and whose
/// confession they can withdraw. Only the voter key is kept. A JSON body is required, which a page on another
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
        if (session.SignedIn(request).Case is not VoterKey voterKey)
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
        if (body is null || Change(body).Case is not Func<ReaderVote, ReaderVote> change)
        {
            return new BadRequestResult();
        }

        var saved = await votes.Vote(number, voterKey, change, cancellationToken);
        // Taken after the save: any page whose counts were read later has this vote (see Script).
        var countedAt = time.GetUtcNow().ToUnixTimeMilliseconds();
        saved.IfRight(counted => counted.IfSome(result => Counted(number, result)));
        var raised = await saved.Match(
            Right: counted => counted.Match(Some: result => Raised(voterKey, result, cancellationToken), None: () => Task.FromResult(Option<RaisedRank>.None)),
            Left: _ => Task.FromResult(Option<RaisedRank>.None));
        return saved.Match(
            Right: counted => counted.Match<IActionResult>(
                Some: shown => new OkObjectResult(new
                {
                    blessed = shown.Counts.Blessed,
                    heresy = shown.Counts.Heresy,
                    confessions = shown.Counts.Confessions,
                    vote = Name(shown.After.Reaction),
                    confessed = shown.After.Confessed,
                    // Null unless the vote raised the reader's rank.
                    rank = raised.MatchUnsafe(Some: office => (object)new { office = office.Office, next = office.Next, insignia = office.Insignia }, None: () => null),
                    countedAt,
                }),
                None: () => new NotFoundResult()),
            Left: error =>
            {
                logger.LogError(error.ToException(), "A vote on rite NO. {Number} couldn't be saved: {Reason}", number, error.Message);
                return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
            });
    }

    /// <summary>
    /// The signed-in reader's votes on the rites in <c>?rites=12,13</c>, so the page can show them pressed: <c>{ "12":
    /// "blessed", "confessed": [12, 13] }</c>.
    /// </summary>
    [Function("Votes")]
    public async Task<IActionResult> Votes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/votes")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        NeverCached(request);
        if (session.SignedIn(request).Case is not VoterKey voterKey)
        {
            return new UnauthorizedResult();
        }
        var rites = RiteNumbers(request.Query["rites"].ToString());
        if (rites.IsNone)
        {
            return new BadRequestResult();
        }

        return (await votes.GetVotes(voterKey, rites.IfNone([]), cancellationToken)).Match<IActionResult>(
            Right: found => new OkObjectResult(Shown(found)),
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

    // A vote that gives the reader a say on a new rite may raise their rank (#201): only then are their rites counted.
    // A count that fails costs only the toast; the Breviary shows the rank anyway.
    private async Task<Option<RaisedRank>> Raised(VoterKey voterKey, SavedVote saved, CancellationToken cancellationToken)
    {
        if (!saved.Before.IsNothing || saved.After.IsNothing)
        {
            return None;
        }
        return (await votes.CountRites(voterKey, cancellationToken)).Match(
            Right: judged => Ranks.Of(judged) == Ranks.Of(judged - 1)
                ? None
                : Some(new RaisedRank($"Thou art now {Ranks.Of(judged).Article} {Ranks.Of(judged).Name}.", $"{Ranks.ToNext(judged)}.", Ranks.Of(judged).Insignia)),
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "A reader's rites couldn't be counted for their rank: {Reason}", error.Message);
                return Option<RaisedRank>.None;
            });
    }

    // Only a vote that changed something: the same vote sent again changes no count, and no page. The verdict and the
    // confession are counted apart, as readers give them apart.
    private void Counted(int number, SavedVote saved)
    {
        if (!saved.Changed)
        {
            return;
        }
        purges.Add(Paths.ShowingVotesOf(number));
        if (saved.Before.Reaction != saved.After.Reaction)
        {
            logger.CountVote(Name(saved.After.Reaction) ?? "withdrawn");
        }
        if (saved.Before.Confessed != saved.After.Confessed)
        {
            logger.CountConfession(saved.After.Confessed ? "confessed" : "withdrawn");
        }
    }

    // What the body asks to change: the verdict, the confession, or both, each as it was when absent. None for a vote
    // that names no verdict, a confession that isn't true or false, or a body that asks for nothing.
    private static Option<Func<ReaderVote, ReaderVote>> Change(VoteRequest body)
        => body.Vote.ValueKind == JsonValueKind.Undefined && body.Confessed.ValueKind == JsonValueKind.Undefined
            ? None
            : from verdict in Verdict(body.Vote)
              from confession in Confession(body.Confessed)
              select (Func<ReaderVote, ReaderVote>)(vote => confession(verdict(vote)));

    // Kept when absent, taken back by null, or exactly the names the page sends, lowercase; anything else isn't a vote.
    private static Option<Func<ReaderVote, ReaderVote>> Verdict(JsonElement vote) => vote.ValueKind switch
    {
        JsonValueKind.Undefined => Some<Func<ReaderVote, ReaderVote>>(kept => kept),
        JsonValueKind.Null => Some<Func<ReaderVote, ReaderVote>>(kept => kept with { Reaction = None }),
        JsonValueKind.String => Named(vote.GetString()).Map<Func<ReaderVote, ReaderVote>>(reaction => kept => kept with { Reaction = Some(reaction) }),
        _ => None,
    };

    private static Option<Reaction> Named(string? name) => name switch
    {
        "blessed" => Some(Reaction.Blessed),
        "heresy" => Some(Reaction.Heresy),
        _ => None,
    };

    // Kept when absent, or exactly true or false; null isn't a confession, nor a way to leave it as it is.
    private static Option<Func<ReaderVote, ReaderVote>> Confession(JsonElement confessed) => confessed.ValueKind switch
    {
        JsonValueKind.Undefined => Some<Func<ReaderVote, ReaderVote>>(kept => kept),
        JsonValueKind.True => Some<Func<ReaderVote, ReaderVote>>(kept => kept with { Confessed = true }),
        JsonValueKind.False => Some<Func<ReaderVote, ReaderVote>>(kept => kept with { Confessed = false }),
        _ => None,
    };

    // The verdicts as they always were, by rite, which a page cached with an older script still reads; the confessions
    // (#200) beside them, under a key no rite number can be.
    private static Dictionary<string, object?> Shown(Dictionary<int, ReaderVote> found)
    {
        var shown = found
            .Where(vote => vote.Value.Reaction.IsSome)
            .ToDictionary(vote => vote.Key.ToString(CultureInfo.InvariantCulture), vote => (object?)Name(vote.Value.Reaction));
        shown["confessed"] = found.Where(vote => vote.Value.Confessed).Select(vote => vote.Key).Order().ToList();
        return shown;
    }

    // Null is the JSON for no vote.
    private static string? Name(Option<Reaction> vote)
        => vote.MatchUnsafe(Some: reaction => reaction == Reaction.Blessed ? "blessed" : "heresy", None: () => null);

    private static void NeverCached(HttpRequest request) => request.HttpContext.Response.Headers.CacheControl = "no-store";
}
