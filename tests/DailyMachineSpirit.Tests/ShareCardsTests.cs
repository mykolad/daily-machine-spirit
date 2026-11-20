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

    // A word wider than the card (a long path in code, a long name in the title) is set smaller, never past its edge
    // and never failing the card.
    [Fact]
    public async Task ARiteWithAWordWiderThanTheCard_StillHasOne()
    {
        await AddRite(
            "The Rite of java.util.concurrent.ConcurrentModificationExceptionAndItsManyCousinsFromTheStandardLibrary",
            "Invoke `/usr/local/lib/node_modules/some-package/node_modules/another-package/dist/cjs/index.js` and wait.");

        var (status, _, png) = await Get(Function(), "1");

        Assert.Equal(StatusCodes.Status200OK, status);
        using var card = SKBitmap.Decode(png);
        Assert.Equal(1200, card.Width);
    }

    // Cloudflare keeps every spelling of an address apart, and an erasure purges only the card's own: no other is served.
    [Theory]
    [InlineData("01", "/r/01/card.png")]
    [InlineData("1", "/R/1/card.png")]
    [InlineData("1", "/r/1/Card.png")]
    public async Task AnotherSpellingOfTheAddress_HasNoCard(string id, string path)
    {
        await AddRite("Press Re-run thrice.");

        Assert.Equal(StatusCodes.Status404NotFound, (await Get(Function(), id, path)).Status);
    }

    // Past every size the design names, a title with a word longer than any title should hold still fits the card.
    [Fact]
    public void ATitleWiderThanEverySize_StillFitsTheCard()
    {
        string[] words = ["The", "Rite", "of", new string('W', 108)];

        var (size, lines) = RiteCards.FittedTitle(words, [.. RiteCards.SmallerTitles], 3);

        using var font = new CardFont(CardAssets.Cinzel, size);
        Assert.All(lines, line => Assert.InRange(font.Measure(line), 0, RiteCards.ContentWidth));
    }

    // A card has no query string of its own: one would be read and drawn again for every new value, so it leads to the
    // card's own address, reading and drawing nothing.
    [Fact]
    public async Task AQueryString_LeadsToTheCardsOwnAddress()
    {
        var request = new DefaultHttpContext().Request;
        request.Path = "/r/1/card.png";
        request.QueryString = new QueryString("?nonce=1");

        var redirect = Assert.IsType<RedirectResult>(await Function().RiteCard(request, "1", CancellationToken.None));

        Assert.Equal(("/r/1/card.png", true), (redirect.Url, redirect.Permanent));
    }

    // An erasure's emoji, which the card's fonts lack, is drawn as Noto Emoji draws it rather than as a missing glyph.
    [Fact]
    public void AnErasuresEmoji_IsDrawnWithTheEmojiFont()
    {
        using var font = new CardFont(CardAssets.Garamond, 48);
        using var emoji = new CardFont(CardAssets.Emoji, 48);

        Assert.Equal(Pixels(emoji, "💀"), Pixels(font, "💀"));
        Assert.NotEqual(Pixels(font, "a"), Pixels(font, "💀"));
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

    private static Task<(int Status, IHeaderDictionary Headers, byte[] Png)> Get(ShareCardsFunction function, string id)
        => Get(function, id, $"/r/{id}/card.png");

    private static async Task<(int Status, IHeaderDictionary Headers, byte[] Png)> Get(ShareCardsFunction function, string id, string path)
    {
        var request = new DefaultHttpContext().Request;
        request.Host = new HostString("dailymachinespirit.test");
        request.Path = path;
        var result = await function.RiteCard(request, id, CancellationToken.None);
        var headers = request.HttpContext.Response.Headers;
        return result switch
        {
            FileContentResult file => (StatusCodes.Status200OK, headers, file.FileContents),
            IStatusCodeActionResult other => (other.StatusCode ?? 0, headers, []),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected result {result}."),
        };
    }

    // The text as drawn, pixel by pixel.
    private static byte[] Pixels(CardFont font, string text)
    {
        using var surface = SKSurface.Create(new SKImageInfo(96, 96));
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        font.Draw(surface.Canvas, text, 8, 72, paint);
        using var image = surface.Snapshot();
        using var pixels = image.PeekPixels();
        return pixels.GetPixelSpan().ToArray();
    }

    private Task AddRite(string text) => AddRite("The Rite of the Elevated Hand", text);

    private async Task AddRite(string title, string text)
        => Ok(await new RiteRepository(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance).Add(new Rite
        {
            PublishedOnUtc = new DateOnly(2026, 10, 9),
            Writing = new RiteWriting
            {
                Kind = RiteKind.Ritual,
                Title = title,
                Text = text,
                HereticalTruth = "Borrowed authority leaves root-owned files behind.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = new DateTime(2026, 10, 8, 23, 0, 0, DateTimeKind.Utc),
            },
        }, CancellationToken.None));
}
