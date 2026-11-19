using System.Globalization;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DailyMachineSpirit.Functions.ShareCards;

/// <summary>
/// The pictures link previews show (#191): a rite's card, drawn on request and kept at Cloudflare until an erasure
/// changes the rite and purges it with its page; and the site's card, one file the design rendered.
/// </summary>
public sealed class ShareCardsFunction
{
    private readonly IRiteReader rites;
    private readonly IRiteCards cards;
    private readonly ILogger<ShareCardsFunction> logger;

    public ShareCardsFunction(IRiteReader rites, IRiteCards cards, ILogger<ShareCardsFunction> logger)
    {
        this.rites = rites;
        this.cards = cards;
        this.logger = logger;
    }

    [Function("RiteCard")]
    public async Task<IActionResult> RiteCard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "r/{id}/card.png")] HttpRequest request,
        string id,
        CancellationToken cancellationToken)
    {
        // At its one address only: Cloudflare keeps every spelling apart (/r/01/card.png, /R/1/card.png), and an erasure
        // purges just this one, so another would show the erased words for its hour.
        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1
            || !string.Equals(request.Path.Value, Paths.RiteCard(number), StringComparison.Ordinal))
        {
            return NotServed(request, StatusCodes.Status404NotFound);
        }
        var found = await rites.GetByNumbers([number], cancellationToken);
        return found.Match<IActionResult>(
            Right: matches => matches.HeadOrNone().Match<IActionResult>(
                Some: rite => Png(request, cards.Draw(rite, request.Host.Value ?? string.Empty), EdgeLifetime.UntilPurged),
                None: () => NotServed(request, StatusCodes.Status404NotFound)),
            Left: error =>
            {
                logger.LogError(error.ToException(), "Rite NO. {Number}'s share card couldn't be drawn: {Reason}", number, error.Message);
                return NotServed(request, StatusCodes.Status503ServiceUnavailable);
            });
    }

    // It changes only with a deployment, which no purge follows: an hour at Cloudflare is the most it can be stale.
    [Function("SiteCard")]
    public IActionResult SiteCard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "card.png")] HttpRequest request)
        => Png(request, CardAssets.SiteCard, EdgeLifetime.UntilPurged);

    // As the pages: an address with a query string is cached apart and no purge names it, so it stays brief.
    private static FileContentResult Png(HttpRequest request, byte[] png, EdgeLifetime edge)
    {
        var headers = request.HttpContext.Response.Headers;
        (request.QueryString.HasValue ? EdgeLifetime.Brief : edge).WriteTo(headers);
        headers.XContentTypeOptions = "nosniff";
        return new FileContentResult(png, "image/png");
    }

    private static StatusCodeResult NotServed(HttpRequest request, int status)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        return new StatusCodeResult(status);
    }
}
