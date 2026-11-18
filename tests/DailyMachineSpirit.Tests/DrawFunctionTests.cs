using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

public sealed class DrawFunctionTests : IAsyncLifetime
{
    private readonly CosmosTestContainer cosmos = new();

    public ValueTask InitializeAsync() => cosmos.InitializeAsync();

    public ValueTask DisposeAsync() => cosmos.DisposeAsync();

    // Without the script, the lots fall on the server: a rite at random, its page announcing the draw. Never kept, or
    // every reader would be sent to the same rite.
    [Fact]
    public async Task Draw_LeadsToARiteAtRandom_NeverKept()
    {
        for (var day = 1; day <= 5; day++)
        {
            await Add(new DateOnly(2026, 10, day));
        }

        var drawn = new List<string>();
        for (var lot = 0; lot < 20; lot++)
        {
            var context = new DefaultHttpContext();
            var result = Assert.IsType<RedirectResult>(await Function(Repository, new Random(lot)).Draw(context.Request, CancellationToken.None));
            Assert.False(result.Permanent);
            Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
            drawn.Add(result.Url);
        }

        Assert.All(drawn, url => Assert.Matches(@"^/r/[1-5]\?drawn$", url));
        Assert.True(drawn.Distinct().Count() > 1, "Twenty draws all fell on the same rite.");
    }

    // Before the first rite there's nothing to draw: Today says when there will be.
    [Fact]
    public async Task Draw_BeforeTheFirstRite_LeadsToToday()
    {
        var result = Assert.IsType<RedirectResult>(await Function(Repository, new Random(1)).Draw(new DefaultHttpContext().Request, CancellationToken.None));

        Assert.Equal("/", result.Url);
    }

    [Fact]
    public async Task Draw_WhenTheRitesCantBeRead_FailsWith503_AndOffersToTryAgain()
    {
        var missing = new RiteRepository(cosmos.Container.Database.GetContainer("no-such-container"), TimeProvider.System, NullLogger<RiteRepository>.Instance);
        var context = new DefaultHttpContext();

        var result = Assert.IsType<ContentResult>(await Function(missing, new Random(1)).Draw(context.Request, CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Contains("""href="/draw">""", result.Content);
    }

    private RiteRepository Repository => new(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance);

    private static DrawFunction Function(IRiteReader rites, Random random) => new(rites, random, NullLogger<DrawFunction>.Instance);

    private async Task Add(DateOnly day)
        => Ok(await Repository.Add(new Rite
        {
            PublishedOnUtc = day,
            Writing = new RiteWriting
            {
                Kind = RiteKind.Ritual,
                Title = $"The Rite of {day:MMMM d}",
                Text = "Press Re-run thrice.",
                HereticalTruth = "Your test depends on `timing`.",
                GeneratedByModel = "gpt-6-sol",
                GeneratedAtUtc = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            },
        }, CancellationToken.None));
}
