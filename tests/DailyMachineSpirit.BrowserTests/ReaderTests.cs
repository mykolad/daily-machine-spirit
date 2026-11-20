using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Voters;
using DailyMachineSpirit.Tests;
using LanguageExt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using static LanguageExt.Prelude;
using static Microsoft.Playwright.Assertions;

namespace DailyMachineSpirit.BrowserTests;

/// <summary>
/// What the page script does for a reader, in a real browser: the parts that regressed or drew review findings, which
/// the server's tests can't reach. Every test also holds the script to running without an error.
/// </summary>
public sealed partial class ReaderTests : IClassFixture<Chromium>, IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 11, 20);

    private readonly Chromium chromium;
    private readonly CosmosTestContainer cosmos = new();
    private readonly List<string> scriptErrors = [];
    private Option<(SiteHost Host, IBrowserContext Context, IPage Page)> opened = None;

    public ReaderTests(Chromium chromium)
    {
        this.chromium = chromium;
    }

    private SiteHost Host => Opened.Host;

    private IBrowserContext Context => Opened.Context;

    private IPage Page => Opened.Page;

    private (SiteHost Host, IBrowserContext Context, IPage Page) Opened
        => opened.IfNone(() => throw new InvalidOperationException("The site is opened in InitializeAsync."));

    public async ValueTask InitializeAsync()
    {
        await cosmos.InitializeAsync();
        var host = await SiteHost.Start(cosmos.Container);
        var context = await chromium.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = host.Address,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        });
        var page = await context.NewPageAsync();
        page.PageError += (_, error) => scriptErrors.Add(error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                scriptErrors.Add(message.Text);
            }
        };
        opened = (host, context, page);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (host, context, _) in opened)
        {
            await context.DisposeAsync();
            await host.DisposeAsync();
        }
        await cosmos.DisposeAsync();
    }

    [Fact]
    public async Task BreakingTheSeal_RevealsTheTruth()
    {
        await AddRites(1);

        await Page.GotoAsync("/");
        await Page.ClickAsync(".seal");

        await Expect(Page.Locator("#truth-panel")).ToBeVisibleAsync();
        await Expect(Page.Locator("#truth-panel")).ToContainTextAsync("The truth of rite 1.");
        await Expect(Page.Locator(".seal")).ToHaveAttributeAsync("aria-expanded", "true");
        Assert.Empty(scriptErrors);
    }

    // A broken seal marks the rite in the Archive; "Hide witnessed" hides it; Forget asks, then clears every mark.
    [Fact]
    public async Task AWitnessedRite_IsMarkedInTheArchive_UntilForgotten()
    {
        await AddRites(3);

        await Page.GotoAsync("/r/2");
        await Page.ClickAsync(".seal");
        await Expect(Page.Locator("#truth-panel")).ToBeVisibleAsync();
        await Page.GotoAsync("/archive");

        var witnessed = Row(2);
        await Expect(witnessed).ToHaveClassAsync(WitnessedClass());
        await Expect(Row(1)).Not.ToHaveClassAsync(WitnessedClass());
        await Expect(Page.Locator(".margin-witnessed .witnessed-count")).ToHaveTextAsync("1 of 3 rites");

        await Page.Locator(".margin-witnessed .witnessed-only").ClickAsync();
        await Expect(witnessed).ToBeHiddenAsync();
        await Page.Locator(".margin-witnessed .witnessed-only").ClickAsync();
        await Expect(witnessed).ToBeVisibleAsync();

        // Asking forgets nothing; Keep goes back to "Forget them…", and the rite is still witnessed on the next visit.
        await Page.Locator(".margin-witnessed [data-forget-ask]").ClickAsync();
        await Expect(Page.Locator(".margin-witnessed [data-forget-keep]")).ToBeFocusedAsync();
        await Expect(witnessed).ToHaveClassAsync(WitnessedClass());
        await Page.Locator(".margin-witnessed [data-forget-keep]").ClickAsync();
        await Expect(Page.Locator(".margin-witnessed [data-forget-ask]")).ToBeFocusedAsync();
        await Page.ReloadAsync();
        await Expect(Row(2)).ToHaveClassAsync(WitnessedClass());

        await Page.Locator(".margin-witnessed [data-forget-ask]").ClickAsync();
        await Page.Locator(".margin-witnessed [data-forget-witnessed]").ClickAsync();
        await Expect(witnessed).Not.ToHaveClassAsync(WitnessedClass());
        await Expect(Page.Locator(".margin-witnessed .witnessed-count")).ToHaveTextAsync("0 of 3 rites");
        Assert.Empty(scriptErrors);
    }

    // A signed-in reader's vote is pressed and counted at once, and is still theirs on the next visit.
    [Fact]
    public async Task ASignedInReadersVote_IsCounted_AndKept()
    {
        await AddRites(1);
        await SignIn("583231");

        await Page.GotoAsync("/");
        // As the reader sees it: the page says they're voting with their account once it knows who they are.
        await Expect(Page.Locator(".vote-status[data-signed-in=true]")).ToBeVisibleAsync();
        var blessed = Page.Locator(".reaction-blessed");
        await blessed.ClickAsync();

        await Expect(blessed).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(blessed.Locator(".count")).ToHaveTextAsync("1");
        // Pressed at once, saved when the page says so.
        await Expect(Page.Locator(".toast-kept")).ToContainTextAsync("Blessed. Kept in your Breviary");
        await Page.ReloadAsync();
        await Expect(Page.Locator(".reaction-blessed")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator(".reaction-blessed .count")).ToHaveTextAsync("1");
        Assert.Empty(scriptErrors);
    }

    // The next page of the Archive comes in place, into the sheet, and a keyboard's focus moves to the first new rite.
    [Fact]
    public async Task LoadingOlderRites_AddsThemInPlace()
    {
        var pageSize = Settings.PublicPages.ArchivePageSize;
        await AddRites(pageSize + 3);

        // The next page is held until Enter is pressed: focusing the link scrolls it into view, which starts the same load
        // on its own, and the keyboard's Enter must still move focus when that load ends.
        var entered = new TaskCompletionSource();
        await Page.RouteAsync("**/archive?before=*", async route =>
        {
            await entered.Task;
            await route.ContinueAsync();
        });
        await Page.GotoAsync("/archive");
        await Expect(Page.Locator(".rows > .row")).ToHaveCountAsync(pageSize);
        await Page.Locator(".archive-more a").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        entered.SetResult();

        await Expect(Page.Locator(".rows > .row")).ToHaveCountAsync(pageSize + 3);
        await Expect(Page.Locator(".archive-loading")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".archive-more")).ToContainTextAsync("The archive ends here.");
        await Expect(Row(3).Locator(".row-title")).ToBeFocusedAsync();
        Assert.Empty(scriptErrors);
    }

    // The dice lead to another rite than the one shown, which the page announces.
    [Fact]
    public async Task DrawingARite_LeadsToAnother_AndSaysSo()
    {
        await AddRites(3);
        // The script draws in the browser; the server's /draw, its fallback when that fails, is closed here, so only a
        // draw the script made can pass.
        await Page.RouteAsync("**/draw", route => route.AbortAsync());

        await Page.GotoAsync("/");
        await Page.Locator(".nav-draw").ClickAsync();

        await Page.WaitForURLAsync(DrawnRite());
        await Expect(Page.Locator(".toast-region")).ToContainTextAsync("The lots have fallen");
        Assert.Empty(scriptErrors);
    }

    // Guessing the truth (#199): the seal waits for a choice, then a right guess folds the readings away and says so;
    // the truth is in the panel alone, which takes the focus and names the true reading.
    [Fact]
    public async Task ARightGuess_IsSeenThrough_AndTheTruthShowsOnce()
    {
        await AddRites(1, Some(new FalseTruths("A false one.", "Another false one.")));

        await Page.GotoAsync("/");
        await Expect(Page.Locator(".seal")).ToHaveAttributeAsync("aria-disabled", "true");
        // A click while it waits does nothing (Playwright, seeing aria-disabled, has to be told to click at all).
        await Page.Locator(".seal").ClickAsync(new LocatorClickOptions { Force = true });
        await Expect(Page.Locator("#truth-panel")).ToBeHiddenAsync();
        var truth = Number(await Page.Locator("[data-guess]").GetAttributeAsync("data-truth"));
        await Page.Locator(".reading", new PageLocatorOptions { HasText = "The truth of rite 1." }).ClickAsync();
        await Expect(Page.Locator(".seal-sub")).ToHaveTextAsync($"Stake thy guess on {Numeral(truth)}");
        await Page.Locator(".seal").ClickAsync();

        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("THOU HAST SEEN THROUGH THE RITE");
        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("No rite shall bind thee. Seen through 1 of 1.");
        await Expect(Page.Locator("[data-verdict]")).Not.ToContainTextAsync("Thou didst choose");
        await Expect(Page.Locator(".guess fieldset")).ToBeHiddenAsync();
        await Expect(Page.Locator("#truth-panel")).ToBeFocusedAsync();
        // Every reading folded away, no numeral names one.
        await Expect(Page.Locator("#truth-heading")).ToHaveTextAsync("Heretical Truth");
        Assert.Empty(scriptErrors);
    }

    // A wrong guess stays on the parchment, struck; one guess a rite, so it opens the same on the next visit.
    [Fact]
    public async Task AWrongGuess_StaysStruck_AndStaysOnTheNextVisit()
    {
        await AddRites(1, Some(new FalseTruths("A false one.", "Another false one.")));

        await Page.GotoAsync("/");
        await Page.Locator(".reading", new PageLocatorOptions { HasText = "Another false one." }).ClickAsync();
        await Page.Locator(".seal").ClickAsync();
        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("THOU ART ONE OF US");
        await Page.ReloadAsync();

        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("The rite would have worked on thee. Seen through 0 of 1.");
        var shown = Page.Locator(".reading:visible");
        await Expect(shown).ToHaveCountAsync(1);
        await Expect(shown).ToHaveClassAsync(MissedClass());
        await Expect(shown).ToContainTextAsync("Another false one. · THY GUESS");
        // Beside the struck guess, the panel names which reading was true.
        var truth = Number(await Page.Locator("[data-guess]").GetAttributeAsync("data-truth"));
        await Expect(Page.Locator("#truth-heading")).ToHaveTextAsync($"Heretical Truth · {Numeral(truth)}.");
        await Expect(Page.Locator("#truth-panel")).ToBeVisibleAsync();
        await Expect(Page.Locator(".seal")).ToHaveAttributeAsync("aria-expanded", "true");
        Assert.Empty(scriptErrors);
    }

    // "Just show me" opens the truth without a guess, and keeps nothing: the step is back on the next visit.
    [Fact]
    public async Task JustShowMe_RevealsWithoutAGuess_AndKeepsNothing()
    {
        await AddRites(1, Some(new FalseTruths("A false one.", "Another false one.")));

        await Page.GotoAsync("/");
        await Page.Locator("[data-guess-skip]").ClickAsync();

        await Expect(Page.Locator("[data-verdict]")).ToHaveTextAsync("Unguessed. The Truth lies below.");
        await Expect(Page.Locator("#truth-panel")).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Expect(Page.Locator(".guess fieldset")).ToBeVisibleAsync();
        await Expect(Page.Locator(".seal")).ToHaveAttributeAsync("aria-disabled", "true");
        Assert.Empty(scriptErrors);
    }

    private ILocator Row(int number)
        => Page.Locator(".rows > .row", new PageLocatorOptions { Has = Page.Locator($"a.row-title[href='/r/{number}']") });

    private async Task SignIn(string accountId)
        => await Context.AddCookiesAsync(
        [
            new Cookie { Name = VoterSession.CookieName, Value = Host.SessionOf(accountId), Url = Host.Address },
            new Cookie { Name = "dms-signed-in", Value = "1", Url = Host.Address },
        ]);

    // Rites 1 to count, a day apart, the last of them today's.
    private Task AddRites(int count) => AddRites(count, None);

    // Rites 1 to count, each with these false truths (#199), or none.
    private async Task AddRites(int count, Option<FalseTruths> falseTruths)
    {
        var rites = new RiteRepository(cosmos.Container, TimeProvider.System, NullLogger<RiteRepository>.Instance);
        for (var number = 1; number <= count; number++)
        {
            await rites.Add(new Rite
            {
                PublishedOnUtc = Day.AddDays(number - count),
                Writing = new RiteWriting
                {
                    Kind = RiteKind.Prayer,
                    Title = $"Rite {number}",
                    Text = $"The words of rite {number}.",
                    HereticalTruth = $"The truth of rite {number}.",
                    FalseTruths = falseTruths,
                    GeneratedByModel = "gpt-6-sol",
                    GeneratedAtUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                },
            }, CancellationToken.None);
        }
    }

    // A reading's numeral as the page writes it, without its full stop.
    private static string Numeral(int place) => new[] { "I", "II", "III" }[place - 1];

    private static int Number(string? value) => int.Parse(value ?? "0", System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\bwitnessed\b")]
    private static partial Regex WitnessedClass();

    [GeneratedRegex(@"\bmissed\b")]
    private static partial Regex MissedClass();

    [GeneratedRegex(@"/r/[12]$")]
    private static partial Regex DrawnRite();
}
