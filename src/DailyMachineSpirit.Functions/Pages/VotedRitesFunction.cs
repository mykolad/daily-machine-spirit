using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Voters;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// <c>/my-rites</c>: the rites the signed-in reader voted on (<see cref="VotedRitesPage"/>). Unlike every other page it
/// differs by reader, so it's never cached, by the browser or by Cloudflare.
/// </summary>
public sealed class VotedRitesFunction
{
    private readonly IVoteRepository votes;
    private readonly IRiteReader rites;
    private readonly VoterSession session;
    private readonly ILogger<VotedRitesFunction> logger;

    public VotedRitesFunction(IVoteRepository votes, IRiteReader rites, VoterSession session, ILogger<VotedRitesFunction> logger)
    {
        this.votes = votes;
        this.rites = rites;
        this.session = session;
        this.logger = logger;
    }

    [Function("VotedRites")]
    public async Task<IActionResult> VotedRites(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "my-rites")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var only = request.Query["verdict"].ToString() switch
        {
            VotedRitesPage.BlessedVerdict => Some(Reaction.Blessed),
            VotedRitesPage.HeresyVerdict => Some(Reaction.Heresy),
            _ => None,
        };
        return await session.SignedIn(request).MatchAsync(
            Some: async voterKey => (await votes.GetAllVotes(voterKey, cancellationToken)
                    .BindAsync(yours => rites.GetByNumbers(yours.Keys.ToList(), cancellationToken)
                        .MapAsync(found => VotedRitesPage.Render(found.OrderByDescending(rite => rite.Number).ToList(), yours, only))))
                .Match(
                    Right: html => Personal(request, StatusCodes.Status200OK, html),
                    Left: error =>
                    {
                        // The path alone: nothing that tells the reader apart.
                        logger.LogError(error.ToException(), "The page {Path} couldn't be served: {Reason}", request.Path.Value, error.Message);
                        return Personal(request, StatusCodes.Status503ServiceUnavailable, StatePages.Failed(Paths.VotedRites));
                    }),
            None: () => Personal(request, StatusCodes.Status200OK, VotedRitesPage.SignedOut()));
    }

    // Cloudflare reads its own header before Cache-Control, so both say so.
    private static IActionResult Personal(HttpRequest request, int status, string html)
    {
        var headers = request.HttpContext.Response.Headers;
        headers.CacheControl = "private, no-store";
        headers["Cloudflare-CDN-Cache-Control"] = "no-store";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}
