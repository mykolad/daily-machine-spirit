using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.ShareCards;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

// The pictures link previews show (#191): a rite's card, drawn from the rite, and the site's.
public sealed class ShareCardsTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();

    public ValueTask InitializeAsync() => cosmos.InitializeAsync();

    public ValueTask DisposeAsync() => cosmos.DisposeAsync();

    [Fact]
    public async Task ARitesCard_IsAPng_OfALargePreviewsSize_KeptUntilPurged()
    {
        await AddRite("When thy installation is denied, invoke `sudo npm install`, O Machine Spirit.");

        var (status, headers, png) = await Get(Function(), "1");

        Assert.Equal(StatusCodes.Status200OK, status);
        using var card = SKBitmap.Decode(png);
        Assert.Equal((1200, 630), (card.Width, card.Height));
        Assert.Equal(EdgeLifetime.UntilPurged.ForCloudflare, headers["Cloudflare-CDN-Cache-Control"].ToString());
    }

    // Around the sheet the card is clear, so a chat's own background shows; the sheet itself is solid.
    [Fact]
    public async Task ARitesCard_IsClearAroundTheSheet()
    {
        await AddRite("Press Re-run thrice.");

        var (_, _, png) = await Get(Function(), "1");

        using var card = SKBitmap.Decode(png);
        Assert.Equal((0, 255), (card.GetPixel(2, 2).Alpha, card.GetPixel(600, 300).Alpha));
    }

    // Far past what fits at the smallest size: the card shows the title alone, still a card.
    [Fact]
    public async Task ARiteTooLongForItsCard_StillHasOne()
    {
        await AddRite(string.Concat(Enumerable.Repeat("O Machine Spirit, grant that the build proceed without question. ", 12)));

        var (status, _, png) = await Get(Function(), "1");

        Assert.Equal(StatusCodes.Status200OK, status);
        using var card = SKBitmap.Decode(png);
        Assert.Equal(1200, card.Width);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("one")]
    public async Task NoSuchRite_HasNoCard(string id)
    {
        await AddRite("Press Re-run thrice.");

        var (status, headers, _) = await Get(Function(), id);

        Assert.Equal((StatusCodes.Status404NotFound, "no-store"), (status, headers.CacheControl.ToString()));
    }

    [Fact]
    public async Task WhenTheRitesCantBeRead_Is503()
    {
        var missing = new ShareCardsFunction(
            new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), TimeProvider.System, NullLogger<RiteRepository>.Instance),
            new RiteCards(), NullLogger<ShareCardsFunction>.Instance);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, (await Get(missing, "1")).Status);
    }

    [Fact]
    public void TheSitesCard_IsAPng_OfALargePreviewsSize()
    {
        var request = new DefaultHttpContext().Request;

        var file = Assert.IsType<FileContentResult>(Function().SiteCard(request));

        Assert.Equal("image/png", file.ContentType);
        using var card = SKBitmap.Decode(file.FileContents);
        Assert.Equal((1200, 630), (card.Width, card.Height));
    }

    private ShareCardsFunction Function()
        => new(new RiteRepository(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance), new RiteCards(),
            NullLogger<ShareCardsFunction>.Instance);

    private static async Task<(int Status, IHeaderDictionary Headers, byte[] Png)> Get(ShareCardsFunction function, string id)
    {
        var request = new DefaultHttpContext().Request;
        request.Host = new HostString("dailymachinespirit.test");
        var result = await function.RiteCard(request, id, CancellationToken.None);
        var headers = request.HttpContext.Response.Headers;
        return result switch
        {
            FileContentResult file => (StatusCodes.Status200OK, headers, file.FileContents),
            IStatusCodeActionResult other => (other.StatusCode ?? 0, headers, []),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected result {result}."),
        };
    }

    private async Task AddRite(string text)
        => Ok(await new RiteRepository(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance).Add(new Rite
        {
            PublishedOnUtc = new DateOnly(2026, 10, 9),
            Writing = new RiteWriting
            {
                Kind = RiteKind.Ritual,
                Title = "The Rite of the Elevated Hand",
                Text = text,
                HereticalTruth = "Borrowed authority leaves root-owned files behind.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = new DateTime(2026, 10, 8, 23, 0, 0, DateTimeKind.Utc),
            },
        }, CancellationToken.None));
}
