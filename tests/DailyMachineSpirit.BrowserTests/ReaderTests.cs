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

    // A confession (#200) lights the candle and is counted at once, apart from the verdict, and is still the reader's on
    // the next visit; pressed again, it's withdrawn.
    [Fact]
    public async Task ASignedInReadersConfession_IsCounted_ApartFromTheVerdict_AndKept()
    {
        await AddRites(1);
        await SignIn("583231");

        await Page.GotoAsync("/");
        await Expect(Page.Locator(".vote-status[data-signed-in=true]")).ToBeVisibleAsync();
        await Page.Locator(".reaction-blessed").ClickAsync();
        await Expect(Page.Locator(".toast-kept")).ToBeVisibleAsync();
        var confess = Page.Locator("[data-confess]");
        await confess.ClickAsync();

        await Expect(confess).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator(".confessed-count")).ToHaveTextAsync("Thou alone hast confessed");
        await Expect(Page.Locator(".confess-status")).ToHaveTextAsync("Thy sin is recorded. Thou art the first to confess it.");
        await Page.ReloadAsync();
        await Expect(confess).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator(".reaction-blessed")).ToHaveAttributeAsync("aria-pressed", "true");
        await confess.ClickAsync();
        await Expect(Page.Locator(".confess-status")).ToHaveTextAsync("Thy confession is withdrawn.");
        await Expect(Page.Locator(".confessed-count")).ToHaveTextAsync("None have confessed");
        await Page.ReloadAsync();
        await Expect(Page.Locator(".vote-status[data-signed-in=true]")).ToBeVisibleAsync();
        await Expect(confess).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(Page.Locator(".reaction-blessed")).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Empty(scriptErrors);
    }

    // Either answer brings every count, and the page shows them all: a blessing and confession made elsewhere since the
    // page loaded show after the reader confesses, and another confession after they bless.
    [Fact]
    public async Task EitherAnswer_RefreshesEveryCount()
    {
        await AddRites(1);
        await SignIn("583231");
        var votes = new VoteRepository(cosmos.Container);

        await Page.GotoAsync("/");
        await Expect(Page.Locator(".vote-status[data-signed-in=true]")).ToBeVisibleAsync();
        Assert.True((await votes.Vote(1, new VoterKey("someone-else"), vote => new ReaderVote(Some(Reaction.Blessed), true), CancellationToken.None)).IsRight);
        await Page.Locator("[data-confess]").ClickAsync();
        await Expect(Page.Locator(".reaction-blessed .count")).ToHaveTextAsync("1");
        await Expect(Page.Locator(".confessed-count")).ToHaveTextAsync("2 have confessed");
        await Expect(Page.Locator(".reaction-blessed")).ToHaveAttributeAsync("aria-pressed", "false");

        Assert.True((await votes.Vote(1, new VoterKey("a-third"), vote => vote with { Confessed = true }, CancellationToken.None)).IsRight);
        await Page.Locator(".reaction-heresy").ClickAsync();
        await Expect(Page.Locator(".confessed-count")).ToHaveTextAsync("3 have confessed");
        await Expect(Page.Locator("[data-confess]")).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Empty(scriptErrors);
    }

    // A confession and a blessing sent at once, their answers arriving in reverse: the confession's, its counts read
    // before the blessing, doesn't undo the blessing's count.
    [Fact]
    public async Task AnAnswerArrivingLate_DoesntUndoANewerCount()
    {
        await AddRites(1);
        await SignIn("583231");
        var blessed = new TaskCompletionSource();
        await Page.RouteAsync("**/api/rites/1/vote", async route =>
        {
            var confessing = route.Request.PostData?.Contains("confessed", StringComparison.Ordinal) == true;
            var answer = await route.FetchAsync();
            if (confessing)
            {
                await blessed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            await route.FulfillAsync(new() { Response = answer });
            if (!confessing)
            {
                blessed.TrySetResult();
            }
        });

        await Page.GotoAsync("/");
        await Expect(Page.Locator(".vote-status[data-signed-in=true]")).ToBeVisibleAsync();
        await Page.Locator("[data-confess]").ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        await Page.Locator(".reaction-blessed").ClickAsync();

        await Expect(Page.Locator(".confess-status")).ToContainTextAsync("Thy sin is recorded");
        await Expect(Page.Locator(".reaction-blessed .count")).ToHaveTextAsync("1");
        await Expect(Page.Locator(".reaction-blessed")).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Empty(scriptErrors);
    }

    // Signed out, a press confesses nothing and offers the sign-in beside the button.
    [Fact]
    public async Task ASignedOutReadersConfession_OffersTheSignIn()
    {
        await AddRites(1);

        await Page.GotoAsync("/");
        await Page.Locator("[data-confess]").ClickAsync();

        await Expect(Page.Locator(".confess-status")).ToHaveTextAsync("Sign in with GitHub to confess. Thy name stays thine: only a one-way key is kept.");
        await Expect(Page.Locator(".confess-status a")).ToHaveAttributeAsync("href", "/auth/github?return=%2F");
        await Expect(Page.Locator("[data-confess]")).ToHaveAttributeAsync("aria-pressed", "false");
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

    // Guessing the truth (#199): the first press cracks the seal and unfolds the readings under it, focus on the first;
    // a right guess breaks it, the readings give way to the verdict, and the truth is in the panel alone, focused.
    [Fact]
    public async Task ARightGuess_IsSeenThrough_AndTheTruthShowsOnce()
    {
        await AddRites(1, Some(new FalseTruths("A false one.", "Another false one.")));

        await Page.GotoAsync("/");
        await Expect(Page.Locator("[data-readings]")).ToBeHiddenAsync();
        await Expect(Page.Locator(".seal-sub")).ToHaveTextAsync("Guess the Heretical Truth");
        await Page.Locator(".seal").ClickAsync();
        await Expect(Page.Locator(".seal-sub")).ToHaveTextAsync("Choose the truth to break it");
        await Expect(Page.Locator("[data-reading]").First).ToBeFocusedAsync();
        // Expanded, the seal names what it shows: the readings now, the truth once broken.
        await Expect(Page.Locator(".seal")).ToHaveAttributeAsync("aria-controls", "guess-readings");
        await Expect(Page.Locator("#truth-panel")).ToBeHiddenAsync();
        await Page.Locator("[data-reading]", new PageLocatorOptions { HasText = "The truth of rite 1." }).ClickAsync();

        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("THOU HAST SEEN THROUGH THE RITE");
        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("No rite shall bind thee. Seen through 1 of 1.");
        await Expect(Page.Locator("[data-readings]")).ToBeHiddenAsync();
        await Expect(Page.Locator("[data-guessed]")).ToBeHiddenAsync();
        await Expect(Page.Locator("#truth-panel")).ToBeFocusedAsync();
        await Expect(Page.Locator(".seal")).ToHaveAttributeAsync("aria-controls", "truth-panel");
        // With no reading left to see, no numeral names one.
        await Expect(Page.Locator("#truth-heading")).ToHaveTextAsync("Heretical Truth");
        Assert.Empty(scriptErrors);
    }

    // A wrong guess stays above the truth, struck; one guess a rite, so it opens the same on the next visit, and
    // resealing hides it with the truth without offering the readings again.
    [Fact]
    public async Task AWrongGuess_StaysStruck_AndStaysOnTheNextVisit()
    {
        await AddRites(1, Some(new FalseTruths("A false one.", "Another false one.")));

        await Page.GotoAsync("/");
        await Page.Locator(".seal").ClickAsync();
        await Page.Locator("[data-reading]", new PageLocatorOptions { HasText = "Another false one." }).ClickAsync();
        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("THOU ART ONE OF US");
        await Page.ReloadAsync();

        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("The rite would have worked on thee. Seen through 0 of 1.");
        await Expect(Page.Locator("[data-guessed]")).ToContainTextAsync("Another false one. · THY GUESS");
        await Expect(Page.Locator("[data-readings]")).ToBeHiddenAsync();
        // Beside the struck guess, the panel names which reading was true.
        var truth = Number(await Page.Locator("[data-guess]").GetAttributeAsync("data-truth"));
        await Expect(Page.Locator("#truth-heading")).ToHaveTextAsync($"Heretical Truth · {Numeral(truth)}.");
        await Expect(Page.Locator("#truth-panel")).ToBeVisibleAsync();

        await Page.Locator(".seal").ClickAsync();
        await Expect(Page.Locator("#truth-panel")).ToBeHiddenAsync();
        await Expect(Page.Locator("[data-guessed]")).ToBeHiddenAsync();
        await Expect(Page.Locator(".seal-sub")).ToHaveTextAsync("Thy guess is cast");
        await Page.Locator(".seal").ClickAsync();
        await Expect(Page.Locator("[data-guessed]")).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-readings]")).ToBeHiddenAsync();
        Assert.Empty(scriptErrors);
    }

    // "Just show me" opens the truth without a guess, and keeps nothing: the readings are offered on the next visit,
    // and Esc folds them back without a guess either.
    [Fact]
    public async Task JustShowMe_RevealsWithoutAGuess_AndKeepsNothing()
    {
        await AddRites(1, Some(new FalseTruths("A false one.", "Another false one.")));

        await Page.GotoAsync("/");
        await Page.Locator(".seal").ClickAsync();
        await Page.Locator("[data-guess-skip]").ClickAsync();

        await Expect(Page.Locator("[data-verdict]")).ToHaveTextAsync("Unguessed. The Truth lies below.");
        await Expect(Page.Locator("#truth-panel")).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Page.Locator(".seal").ClickAsync();
        await Expect(Page.Locator("[data-readings]")).ToBeVisibleAsync();
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Page.Locator("[data-readings]")).ToBeHiddenAsync();
        await Expect(Page.Locator(".seal")).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(Page.Locator(".seal")).ToBeFocusedAsync();
        Assert.Empty(scriptErrors);
    }

    // A guess made in another tab meanwhile stands: choosing here shows it, rather than a second try.
    [Fact]
    public async Task AGuessFromAnotherTab_Stands()
    {
        await AddRites(1, Some(new FalseTruths("A false one.", "Another false one.")));

        await Page.GotoAsync("/");
        await Page.Locator(".seal").ClickAsync();
        var truth = Number(await Page.Locator("[data-guess]").GetAttributeAsync("data-truth"));
        await Page.EvaluateAsync($$"""localStorage.setItem('dms-guessed-v1', JSON.stringify({ "1": { right: false, pick: {{truth % 3 + 1}} } }))""");
        await Page.Locator("[data-reading]", new PageLocatorOptions { HasText = "The truth of rite 1." }).ClickAsync();

        await Expect(Page.Locator("[data-verdict]")).ToContainTextAsync("THOU ART ONE OF US");
        await Expect(Page.Locator("[data-guessed]")).ToContainTextAsync("· THY GUESS");
        Assert.Empty(scriptErrors);
    }

    // Pressing the cracked seal again breaks it unguessed, as "Just show me" does.
    [Fact]
    public async Task TheCrackedSeal_PressedAgain_RevealsWithoutAGuess()
    {
        await AddRites(1, Some(new FalseTruths("A false one.", "Another false one.")));

        await Page.GotoAsync("/");
        await Page.Locator(".seal").ClickAsync();
        await Expect(Page.Locator(".seal")).ToHaveAttributeAsync("aria-label", "Break the seal without guessing");
        await Page.Locator(".seal").ClickAsync();

        await Expect(Page.Locator("[data-verdict]")).ToHaveTextAsync("Unguessed. The Truth lies below.");
        await Expect(Page.Locator("#truth-panel")).ToBeFocusedAsync();
        await Expect(Page.Locator("[data-readings]")).ToBeHiddenAsync();
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

    [GeneratedRegex(@"/r/[12]$")]
    private static partial Regex DrawnRite();
}
