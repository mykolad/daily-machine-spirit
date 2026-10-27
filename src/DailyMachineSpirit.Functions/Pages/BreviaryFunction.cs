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
/// <c>/breviary</c>: the rites the signed-in reader voted on (<see cref="BreviaryPage"/>). Unlike every other page it
/// differs by reader, so it's never cached, by the browser or by Cloudflare.
/// </summary>
public sealed class BreviaryFunction
{
    private readonly IVoteRepository votes;
    private readonly IRiteReader rites;
    private readonly VoterSession session;
    private readonly ILogger<BreviaryFunction> logger;

    public BreviaryFunction(IVoteRepository votes, IRiteReader rites, VoterSession session, ILogger<BreviaryFunction> logger)
    {
        this.votes = votes;
        this.rites = rites;
        this.session = session;
        this.logger = logger;
    }

    [Function("Breviary")]
    public async Task<IActionResult> Breviary(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "breviary")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var only = request.Query["verdict"].ToString() switch
        {
            BreviaryPage.BlessedVerdict => Some(Reaction.Blessed),
            BreviaryPage.HeresyVerdict => Some(Reaction.Heresy),
            _ => None,
        };
        return await session.SignedIn(request).MatchAsync(
            Some: async voterKey => (await votes.GetAllVotes(voterKey, cancellationToken)
                    .BindAsync(yours => rites.GetByNumbers(yours.Keys.ToList(), cancellationToken)
                        .MapAsync(found => BreviaryPage.Render(found.OrderByDescending(rite => rite.Number).ToList(), yours, only))))
                .Match(
                    Right: html => Personal(request, StatusCodes.Status200OK, html),
                    Left: error =>
                    {
                        // The path alone: nothing that tells the reader apart.
                        logger.LogError(error.ToException(), "The page {Path} couldn't be served: {Reason}", request.Path.Value, error.Message);
                        return Personal(request, StatusCodes.Status503ServiceUnavailable, StatePages.Failed(Paths.Breviary));
                    }),
            None: () => Personal(request, StatusCodes.Status200OK, BreviaryPage.SignedOut()));
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
