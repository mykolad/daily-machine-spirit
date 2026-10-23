using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

/// <summary>The Scriptorium over HTTP: the page, the actions it posts, and what they refuse.</summary>
public sealed class ScriptoriumFunctionTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task Page_ShowsTheCalendar_AndTheJudgments_ButNotTheAshes()
    {
        await testbed.AddDraft("The Rite of `git push --force`", 0.9f);
        var burned = await testbed.AddDraft("Litany of the Endless Spinner", 0.3f, RiteKind.Ritual);
        Ok(await testbed.Scribes().Burn(burned.Id, "Spinners <again>.", BacklogTestbed.Tester, CancellationToken.None));
        var request = Get("");

        var html = Html(await Function().Page(request, CancellationToken.None), 200);

        Assert.Contains("The Liturgical Calendar", html);
        Assert.Contains("The Rite of <code>git push --force</code>", html);
        Assert.DoesNotContain($"""<h4 id="title-{burned.Id:D}">""", html);
        Assert.DoesNotContain("Restore from the Ashes", html);
        // The top draft can be anointed (to keep it first), but not exalted.
        var top = Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0].Draft.Id;
        Assert.Contains($"/scriptorium/drafts/{top:D}/anoint", html);
        Assert.DoesNotContain($"/scriptorium/drafts/{top:D}/exalt", html);
        Assert.Contains("“Spinners &lt;again&gt;.”", html);
        Assert.Contains("not affiliated with or endorsed by Games Workshop", html);
        Assert.Equal("no-store", request.HttpContext.Response.Headers.CacheControl.ToString());
        Assert.Equal("noindex", request.HttpContext.Response.Headers["X-Robots-Tag"].ToString());
        Assert.Equal("frame-ancestors 'none'", request.HttpContext.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Equal("DENY", request.HttpContext.Response.Headers.XFrameOptions.ToString());
    }

    // So the Scribes see what the calendar covers before it's published: each subject, the most first, narrows it.
    [Fact]
    public async Task Page_ShowsWhatEachDraftIsAbout_AndTheCalendarsSubjects()
    {
        await testbed.AddDraft("The Rite of the Hard Refresh", 0.9f, RiteKind.Ritual, new[] { "caches" });
        await testbed.AddDraft("The Litany of Forgotten Names", 0.8f, RiteKind.Prayer, new[] { "infrastructure", "caches" });
        await testbed.AddDraft("The Unjudged Rite", 0.7f);

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<p class="subjects" role="group" aria-label="Filter by subject"><a class="chip" href="/scriptorium" aria-current="true">All<span class="chip-count">3</span></a><a class="chip" href="/scriptorium?subject=caches">Caches<span class="chip-count">2</span></a><a class="chip" href="/scriptorium?subject=infrastructure">Infrastructure<span class="chip-count">1</span></a></p>""", html);
        Assert.Contains("""Forgotten Names</h4><p class="tags"><span class="visually-hidden">Filed under </span><span class="tag">Infrastructure</span><span class="tag">Caches</span></p>""", html);
        // And the trouble each is for, which the rites API matches on (#115).
        Assert.Contains("""<p class="ailment"><span class="truth-label">Ailment</span> The build is slow every time the cache is cleared.</p>""", html);
    }

    // Narrowed to a subject, each order shows that subject's rites in their own places, and says how many of its own
    // those are; the chosen subject's chip clears it.
    [Fact]
    public async Task Page_NarrowedToASubject_ShowsItsRitesInTheirPlaces()
    {
        var refresh = await testbed.AddDraft("The Rite of the Hard Refresh", 0.9f, RiteKind.Ritual, new[] { "caches" });
        var names = await testbed.AddDraft("The Litany of Forgotten Names", 0.8f, RiteKind.Prayer, new[] { "infrastructure" });
        var cold = await testbed.AddDraft("The Litany of the Cold Cache", 0.7f, RiteKind.Prayer, new[] { "caches" });
        Ok(await testbed.Scribes().Anoint(refresh.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var html = Html(await Function().Page(Get("?subject=caches"), CancellationToken.None), 200);
        var unknown = Html(await Function().Page(Get("?subject=gossip"), CancellationToken.None), 200);

        Assert.Contains("""<a class="chip" href="/scriptorium" aria-current="true">Caches<span class="chip-count">2</span></a>""", html);
        Assert.Contains("""Anointed<span class="group-count">1 of 1</span>""", html);
        Assert.Contains("""The Augury’s order<span class="group-count">1 of 2</span>""", html);
        Assert.Contains($"""id="rite-{cold.Id:D}" aria-labelledby="title-{cold.Id:D}">""", html);
        Assert.DoesNotContain($"""id="rite-{names.Id:D}" """, html);
        // Both rites' forms, Summon's and Let the Augury Decide's.
        Assert.Equal(4, html.Split("""<input type="hidden" name="subject" value="caches">""").Length - 1);
        Assert.Contains($"""id="rite-{names.Id:D}" """, unknown);
        Assert.DoesNotContain("""name="subject" """, unknown);
    }

    [Fact]
    public async Task Page_NarrowedToASubjectWithoutRites_SaysSo_InEachOrder()
    {
        await testbed.AddDraft("The Rite of the Hard Refresh", 0.9f, RiteKind.Ritual, new[] { "caches" });

        var html = Html(await Function().Page(Get("?subject=security"), CancellationToken.None), 200);
        var all = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<a class="chip" href="/scriptorium" aria-current="true">Security<span class="chip-count">0</span></a>""", html);
        Assert.Contains("No Security rites among the anointed.", html);
        Assert.Contains("No Security rites in the Augury’s order.", html);
        Assert.Contains("No rite is anointed yet.", all);
    }

    // An action on the narrowed calendar comes back to it, narrowed.
    [Theory]
    [InlineData("exalt")]
    [InlineData("burn")]
    public async Task AnActionOnANarrowedCalendar_ComesBackNarrowed(string action)
    {
        await testbed.AddDraft("The Rite of the Hard Refresh", 0.9f, RiteKind.Ritual, new[] { "caches" });
        var cold = await testbed.AddDraft("The Litany of the Cold Cache", 0.7f, RiteKind.Prayer, new[] { "caches" });

        var response = await Function().Decide(Post("application/x-www-form-urlencoded", "note=&subject=caches"), cold.Id, action, CancellationToken.None);

        Assert.Contains("&subject=caches", Assert.IsType<RedirectResult>(response.Result).Url);
    }

    // So do Summon and Let the Augury Decide, which act on the whole calendar.
    [Fact]
    public async Task SummonAndTheAugury_OnANarrowedCalendar_ComeBackNarrowed()
    {
        await testbed.AddDraft("The Rite of the Hard Refresh", 0.9f, RiteKind.Ritual, new[] { "caches" });
        var cold = await testbed.AddDraft("The Litany of the Cold Cache", 0.7f, RiteKind.Prayer, new[] { "caches" });
        Ok(await testbed.Scribes().Anoint(cold.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var function = Function();

        var summon = await function.Summon(Post("application/x-www-form-urlencoded", "subject=caches"), CancellationToken.None);
        var augury = await function.LetTheAuguryDecide(Post("application/x-www-form-urlencoded", "subject=caches"), CancellationToken.None);

        Assert.Equal("/scriptorium?done=summon&subject=caches", Assert.IsType<RedirectResult>(summon.Result).Url);
        Assert.Equal("/scriptorium?done=augury&subject=caches", Assert.IsType<RedirectResult>(augury).Url);
    }

    // Amending from a narrowed calendar comes back to it: the link, the page's form and its way back carry the subject.
    [Fact]
    public async Task AmendingFromANarrowedCalendar_ComesBackNarrowed()
    {
        var cold = await testbed.AddDraft("The Litany of the Cold Cache", 0.7f, RiteKind.Prayer, new[] { "caches" });
        var function = Function();

        var calendar = Html(await function.Page(Get("?subject=caches"), CancellationToken.None), 200);
        var page = Html(await function.AmendPage(Get("?subject=caches"), cold.Id, CancellationToken.None), 200);
        var saved = await function.Decide(Post("application/x-www-form-urlencoded",
            $"etag={Uri.EscapeDataString(await ETag(cold))}&title=Warm&text=O%20Machine%20Spirit&truth=A%20warm%20cache&ailment=A%20slow%20build&note=&subject=caches"),
            cold.Id, "amend", CancellationToken.None);

        Assert.Contains($"""href="/scriptorium/drafts/{cold.Id:D}/amend?subject=caches">""", calendar);
        Assert.Contains("""<input type="hidden" name="subject" value="caches">""", page);
        Assert.Contains($"""href="/scriptorium?subject=caches#rite-{cold.Id:D}">Leave It as It Was""", page);
        Assert.Equal($"/scriptorium?done=amend&rite={cold.Id:D}&subject=caches#rite-{cold.Id:D}", Assert.IsType<RedirectResult>(saved.Result).Url);
    }

    // Straight after a burn, the calendar's Undo restores the rite, a Scribes' one too, into the Augury's order.
    [Fact]
    public async Task Undo_PutsABurnedRiteBackInTheAugurysOrder()
    {
        await testbed.AddDraft("First", 0.9f);
        var second = await testbed.AddDraft("Second", 0.8f);
        Ok(await testbed.Scribes().Anoint(second.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var function = Function();
        Ok(await testbed.Scribes().Burn(second.Id, "Duplicate", BacklogTestbed.Tester, CancellationToken.None));

        var response = await function.Decide(Post("application/x-www-form-urlencoded", "note="), second.Id, "restore", CancellationToken.None);

        var calendar = Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar;
        Assert.Equal(["First", "Second"], calendar.Select(entry => entry.Draft.Writing.Title));
        Assert.All(calendar, entry => Assert.False(entry.PlacedByScribes));
        Assert.Equal($"/scriptorium?done=restore&rite={second.Id:D}#rite-{second.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
    }

    [Fact]
    public async Task Page_WithAnEmptyBacklog_SaysSo_AndOffersToSummon()
    {
        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("No rite waits", html);
        Assert.Contains("Summon New Rites", html);
        Assert.DoesNotContain("Let the Augury Decide", html);
    }

    // Each tab says what its page holds: the calendar's, how many rites the Scribes placed and how many the Augury does.
    [Fact]
    public async Task EveryPage_HasTheTabs_CountingWhatEachHolds_AndMarkingItsOwn()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);
        var burned = await testbed.AddDraft("Burned", 0.3f);
        await AnointAll(excellent, good);
        Ok(await testbed.Scribes().Burn(burned.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.PreceptKeeping().AddPinned("Name the command.", BacklogTestbed.Tester, CancellationToken.None));
        var function = Function();
        var precepts = new PreceptsFunction(testbed.PreceptKeeping(), testbed.Distiller(),
            new AgreementTrend(testbed.Scriptorium, Options.Create(Settings.ScriptoriumPages), testbed.Time), testbed.Tabs(),
            testbed.NamesShown(), new EveryoneIsScribeSignIn(), testbed.Time, NullLogger<PreceptsFunction>.Instance);

        var pages = new Dictionary<string, string>
        {
            ["/scriptorium"] = Html(await function.Page(Get(""), CancellationToken.None), 200),
            ["/scriptorium/ashes"] = Html(await function.Ashes(Get(""), CancellationToken.None), 200),
            ["/scriptorium/precepts"] = Html(await precepts.Page(Get(""), CancellationToken.None), 200),
            ["/scriptorium/record"] = Html(await function.Record(Get(""), CancellationToken.None), 200),
        };

        Assert.All(pages, page =>
        {
            // Excellent and Good anointed, Fair in the Augury’s order.
            Assert.Contains("""The Calendar<span class="count"><span aria-hidden="true" title="Anointed / in the Augury’s order">2/1</span><span class="visually-hidden">: 2 anointed, 1 in the Augury’s order</span></span>""", page.Value);
            Assert.Contains("""The Ashes<span class="count">1</span>""", page.Value);
            Assert.Contains("""The Precepts<span class="count">1</span>""", page.Value);
            // Four drafts written, two anointed, one burned, and a precept added.
            Assert.Contains("""The Record<span class="count">8</span>""", page.Value);
            Assert.Contains($"""<a href="{page.Key}" aria-current="page">""", page.Value);
            Assert.Single(page.Value.Split("aria-current=\"page\"").Skip(1));
        });
    }

    // The counts are a nicety: without them, the tabs still lead everywhere.
    [Fact]
    public async Task Tabs_WhenTheyCantBeCounted_StillShow_WithoutCounts()
    {
        var function = new ScriptoriumFunction(
            testbed.Scribes(), testbed.Amendments(), testbed.DraftJudging(), testbed.Audit, testbed.ScribeNaming(), testbed.TabsWithoutCosmos(), Options.Create(Settings.ScriptoriumPages), new EveryoneIsScribeSignIn(), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

        var html = Html(await function.Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<a href="/scriptorium" aria-current="page">The Calendar</a>""", html);
        Assert.Contains("""<a href="/scriptorium/ashes">The Ashes</a>""", html);
        Assert.DoesNotContain("""class="count""", html);
    }

    [Fact]
    public async Task Ashes_WhenNothingBurned_SaySo()
    {
        var html = Html(await Function().Ashes(Get(""), CancellationToken.None), 200);

        Assert.Contains("The Ashes are cold. Nothing has been burned.", html);
    }

    // The page shows the newest; the tab counts them all, so the page says how many more there are.
    [Fact]
    public async Task Ashes_ShowTheNewestBurned_EachToRestore_AndHowManyBurnedInAll()
    {
        for (var i = 0; i <= Settings.ScriptoriumPages.AshesShown; i++)
        {
            testbed.Time.Advance(TimeSpan.FromMinutes(1));
            var draft = await testbed.AddDraft($"Burned {i}", 0.5f);
            Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        }

        var html = Html(await Function().Ashes(Get(""), CancellationToken.None), 200);

        Assert.Contains($"The {Settings.ScriptoriumPages.AshesShown} burned most recently, of {Settings.ScriptoriumPages.AshesShown + 1}.", html);
        Assert.Contains($"Burned {Settings.ScriptoriumPages.AshesShown}", html);
        Assert.DoesNotContain("Burned 0<", html);
        Assert.Equal(Settings.ScriptoriumPages.AshesShown, html.Split("Restore from the Ashes<span").Length - 1);
    }

    // A burn's message links to its rite in the Ashes; followed after newer burns pushed it past the ones shown, the
    // page still shows it, to restore.
    [Fact]
    public async Task Ashes_ShowARiteBurnedLongerAgo_WhenALinkAsksForIt()
    {
        var first = await testbed.AddDraft("Burned first", 0.5f);
        Ok(await testbed.Scribes().Burn(first.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        for (var i = 0; i < Settings.ScriptoriumPages.AshesShown; i++)
        {
            testbed.Time.Advance(TimeSpan.FromMinutes(1));
            var draft = await testbed.AddDraft($"Burned later {i}", 0.5f);
            Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        }

        var html = Html(await Function().Ashes(Get($"?rite={first.Id:D}"), CancellationToken.None), 200);

        Assert.Contains("Burned longer ago<span class=\"group-count\">1</span></h2>", html);
        Assert.Contains($"""<article class="rite row ash acted" id="rite-{first.Id:D}" """, html);
        Assert.Contains($"/scriptorium/drafts/{first.Id:D}/restore", html);
        Assert.Contains($"The {Settings.ScriptoriumPages.AshesShown} burned most recently, of {Settings.ScriptoriumPages.AshesShown + 1}.", html);
    }

    // Grouped by the day they burned, each says who burned it, from where and why, and the reason rides along for the
    // script's Undo of a restore.
    [Fact]
    public async Task Ashes_GroupTheRitesByTheDayTheyBurned_SayingWhoBurnedEach_FromWhereAndWhy()
    {
        var reasoned = await testbed.AddDraft("Too narrow", 0.9f);
        var silent = await testbed.AddDraft("Unexplained", 0.5f);
        var function = Function();
        await function.Decide(Post("Too specific"), reasoned.Id, "burn", CancellationToken.None);
        testbed.Time.Advance(TimeSpan.FromDays(1));
        await function.Decide(Post(""), silent.Id, "burn", CancellationToken.None);

        var html = Html(await function.Ashes(Get(""), CancellationToken.None), 200);

        Assert.True(html.IndexOf("Burned Thu 8 Oct<span class=\"group-count\">1</span>", StringComparison.Ordinal)
            < html.IndexOf("Burned Wed 7 Oct<span class=\"group-count\">1</span>", StringComparison.Ordinal));
        Assert.Contains($"""<article class="rite row ash" id="rite-{reasoned.Id:D}" aria-labelledby="title-{reasoned.Id:D}" data-reason="Too specific">""", html);
        Assert.Contains("""<span class="from">from #1</span>""", html);
        Assert.Contains("""<p class="ailment"><span class="truth-label">Ailment</span> The build is slow every time the cache is cleared.</p>""", html);
        Assert.Contains("""<span class="reason-said"><span class="visually-hidden">Why it burned: </span>“Too specific”</span>""", html);
        Assert.Contains($"""Burned by A Scribe on staging · <a href="/scriptorium/record?draft={reasoned.Id:D}">its record</a>""", html);
        Assert.Contains($"""Burned by A Scribe on staging · no reason given · <a href="/scriptorium/record?draft={silent.Id:D}">its record</a>""", html);
        Assert.Contains("Signed in as <strong>", html);
    }

    [Fact]
    public async Task Ashes_NarrowedToASubject_WithoutItsRites_SaySo()
    {
        var draft = await testbed.AddDraft("Burned", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var html = Html(await Function().Ashes(Get("?subject=caches"), CancellationToken.None), 200);

        Assert.Contains("No Caches rites in the Ashes.", html);
        Assert.Contains("""<a class="chip" href="/scriptorium/ashes">All<span class="chip-count">1</span></a>""", html);
    }

    // Restored from the Ashes, the Scribe stays there, told where the rite went; the Undo burns it again for the same
    // reason, back among the Ashes, marked.
    [Fact]
    public async Task Restore_FromTheAshes_ComesBackToThem_AndBurningAgainKeepsTheReason()
    {
        var draft = await testbed.AddDraft("Second chance", 0.5f);
        var function = Function();
        await function.Decide(Post("Too specific"), draft.Id, "burn", CancellationToken.None);

        var restored = await function.Decide(Post("application/x-www-form-urlencoded", "page=ashes&note="), draft.Id, "restore", CancellationToken.None);

        Assert.Equal($"/scriptorium/ashes?done=restore&rite={draft.Id:D}", Assert.IsType<RedirectResult>(restored.Result).Url);
        var html = Html(await function.Ashes(Following(restored.Result), CancellationToken.None), 200);
        Assert.Contains($"""<span data-undoable>“Second chance” returned to the Augury’s order. <a href="/scriptorium?rite={draft.Id:D}#rite-{draft.Id:D}">See it in the calendar</a>.</span>""", html);
        Assert.DoesNotContain($"id=\"rite-{draft.Id:D}\"", html);
        Assert.True(Ok(await testbed.Drafts.Get(draft.Id, CancellationToken.None)).Exists(waiting => waiting.BurnReason.IsNone));

        var burned = await function.Decide(Post("application/x-www-form-urlencoded", "page=ashes&note=Too+specific"), draft.Id, "burn", CancellationToken.None);

        Assert.Equal($"/scriptorium/ashes?done=burn-last&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(burned.Result).Url);
        var again = Html(await function.Ashes(Following(burned.Result), CancellationToken.None), 200);
        Assert.Contains("“Second chance” is back in the Ashes.", again);
        Assert.Contains($"""<article class="rite row ash acted" id="rite-{draft.Id:D}" aria-labelledby="title-{draft.Id:D}" data-reason="Too specific">""", again);
    }

    [Fact]
    public async Task Restore_FromTheAshes_OfARiteAnotherScribeRestored_SaysSoThere()
    {
        var draft = await testbed.AddDraft("Twice", 0.5f);
        var function = Function();
        await function.Decide(Post(""), draft.Id, "burn", CancellationToken.None);
        await function.Decide(Post("application/x-www-form-urlencoded", "page=ashes&note="), draft.Id, "restore", CancellationToken.None);

        var response = await function.Decide(Post("application/x-www-form-urlencoded", "page=ashes&note="), draft.Id, "restore", CancellationToken.None);

        Assert.Equal($"/scriptorium/ashes?done=changed&rite={draft.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Contains("Another Scribe restored or burned it meanwhile.", Html(await function.Ashes(Following(response.Result), CancellationToken.None), 200));
    }

    [Fact]
    public async Task Page_CarriesTheSitesIcon_Inline()
    {
        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<link rel="icon" type="image/svg+xml" href="data:image/svg+xml,%3Csvg""", html);
    }

    [Fact]
    public async Task Page_UsesTheSitesOwnFonts_AndContactsNoOtherSite()
    {
        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("url(/fonts/cinzel.woff2)", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public async Task Page_SaysWhatTheLastActionDid()
    {
        var html = Html(await Function().Page(Get("?done=burn"), CancellationToken.None), 200);

        Assert.Contains("Consigned to the flames.", html);
    }

    [Theory]
    [InlineData("anoint", "Anointed.")]
    [InlineData("release", "Released to the Augury.")]
    [InlineData("exalt", "Exalted.")]
    [InlineData("humble", "Humbled.")]
    [InlineData("restore", "Restored from the ashes.")]
    [InlineData("burn-last", "<span data-undoable>Consigned to the flames.</span> No rite waits now")]
    [InlineData("summon", "Summoning 3 new rites: they're written over the next few minutes.")]
    [InlineData("augury", "The Augury orders every draft again.")]
    [InlineData("changed", "Another Scribe changed the calendar meanwhile.")]
    [InlineData("unmoved", "It was already there, so nothing moved.")]
    [InlineData("note-too-long", "That note is too long, so nothing was changed.")]
    public async Task Page_SaysWhatTheLastActionDid_EvenWhenTheRiteIsGoneSince(string done, string message)
    {
        var html = Html(await Function().Page(Get($"?done={done}&rite={Guid.NewGuid():D}"), CancellationToken.None), 200);

        Assert.Contains($"""<p class="status" role="status">{message}""", html);
    }

    // A refill writes only what's missing: a full calendar can't summon, and says why; otherwise the page says how many
    // are coming, and tells the script to look for them (data-summoning) by how many wait (data-waiting).
    [Fact]
    public async Task Summon_SaysHowManyAreComing_AndCantBePressedWhenTheCalendarIsFull()
    {
        await testbed.AddDraft("First", 0.9f);
        var summoned = Html(await Function().Page(Get("?done=summon"), CancellationToken.None), 200);
        await testbed.AddDraft("Second", 0.8f);
        await testbed.AddDraft("Third", 0.7f);

        var full = Html(await Function().Page(Get("?done=summon"), CancellationToken.None), 200);

        Assert.Contains("Summoning 2 new rites", summoned);
        Assert.Contains("""<main id="main" data-waiting="1" data-summoning="3">""", summoned);
        Assert.Contains("The calendar is full: 3 rites wait, so there's nothing to summon.", full);
        Assert.Contains("""<main id="main" data-waiting="3">""", full);
        Assert.Contains("""<button title="The calendar is full: 3 rites wait. More are written as rites are published or burned." class="primary" disabled>""", full);
    }

    // Opening or folding every rite is the script's: without it every rite is open, so the pair waits hidden.
    [Fact]
    public async Task Page_OffersToOpenOrFoldEveryRite_WhereTheScriptRuns()
    {
        await testbed.AddDraft("First", 0.9f);

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<span class="pair every" role="group" aria-label="Every rite" hidden>""", html);
        Assert.Contains("""data-fold="open" title="Open every rite shown">""", html);
        Assert.Contains("""<span class="visually-hidden">Fold every rite</span>""", html);
    }

    [Fact]
    public async Task Page_SaysNothing_ForAnUnknownOutcome()
    {
        var html = Html(await Function().Page(Get("?done=smite"), CancellationToken.None), 200);

        Assert.Contains("""<p class="status" role="status"></p>""", html);
    }

    [Theory]
    [InlineData("exalt", "Exalted")]
    [InlineData("anoint", "Anointed")]
    public async Task AfterAMove_ThePageNamesTheRite_AndWhenItNowGoesOut(string action, string done)
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        if (action == "exalt")
        {
            await AnointAll(excellent, good);
        }
        var function = Function();

        var response = await function.Decide(Post(""), good.Id, action, CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);

        var first = Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0];
        Assert.Equal(good.Id, first.Draft.Id);
        Assert.Contains($"{done} “Good”", html);
        Assert.Contains(first.PublishesOnUtc.ToString("ddd d MMM", CultureInfo.InvariantCulture) + ".", html);
        Assert.Contains($"""<article class="rite row scribes next acted" id="rite-{good.Id:D}" """, html);
    }

    [Fact]
    public async Task AfterAHumbling_ThePageSaysWhenTheRiteNowGoesOut()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        await AnointAll(excellent, good);
        var function = Function();

        var response = await function.Decide(Post(""), excellent.Id, "humble", CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);

        var second = Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[1];
        Assert.Equal(excellent.Id, second.Draft.Id);
        Assert.Contains($"Humbled “Excellent” to {second.PublishesOnUtc.ToString("ddd d MMM", CultureInfo.InvariantCulture)}.", html);
    }

    [Fact]
    public async Task AfterABurn_TheMessageLinksToTheRiteInTheAshes_WhichMarkIt()
    {
        var burned = await testbed.AddDraft("Burned", 0.5f);
        await testbed.AddDraft("Still waiting", 0.5f);
        var function = Function();

        var response = await function.Decide(Post(""), burned.Id, "burn", CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);

        var link = $"/scriptorium/ashes?rite={burned.Id:D}#rite-{burned.Id:D}";
        Assert.Contains($"""<span data-undoable>Consigned “Burned” to the flames.</span><span data-undoable> <a href="{link}">Restore it from the Ashes</a>.</span>""", html);
        Assert.DoesNotContain("acted\" id=", html);
        var ashes = Html(await function.Ashes(Get(new Uri(new Uri("https://dailymachinespirit.fyi"), link).Query), CancellationToken.None), 200);
        Assert.Contains($"""<article class="rite row ash acted" id="rite-{burned.Id:D}" """, ashes);
    }

    // So the Scribe's eye finds where it went, with or without the script that slides it there.
    [Fact]
    public async Task AfterAnAction_TheRiteItWasOnIsMarked_AndNoOther()
    {
        var first = await testbed.AddDraft("First", 0.9f);
        var exalted = await testbed.AddDraft("Second", 0.5f);
        await AnointAll(first, exalted);
        var function = Function();

        var response = await function.Decide(Post(""), exalted.Id, "exalt", CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);
        var before = Html(await function.Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"""<article class="rite row scribes next acted" id="rite-{exalted.Id:D}" """, html);
        Assert.Single(html.Split(" acted\" id=").Skip(1));
        Assert.DoesNotContain(" acted\" id=", before);
    }

    // The forms work without it; with it, an action is shown in place.
    [Fact]
    public async Task Page_CarriesTheScriptThatShowsActionsInPlace()
    {
        await testbed.AddDraft("First", 0.9f);

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"<script>{ScriptoriumScript.Js}</script>", html);
    }

    [Fact]
    public async Task AfterARestore_ThePageSaysWhenTheRiteGoesOut()
    {
        var draft = await testbed.AddDraft("Second chance", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var function = Function();

        var response = await function.Decide(Post(""), draft.Id, "restore", CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);

        Assert.Contains("Restored “Second chance” from the ashes: it waits for", html);
    }

    [Fact]
    public async Task Page_TagsTheNextRite_WithWhenItGoesOut()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);

        // Today's rite isn't out yet, so the first is already due: the midnight run's retry publishes it.
        var due = Html(await Function().Page(Get(""), CancellationToken.None), 200);
        Ok(await testbed.Publisher().PublishToday(CancellationToken.None));
        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("· publishes next</span>", due);
        Assert.DoesNotContain("00:00 UTC", due);
        Assert.Single(html.Split("""<span class="next-label">Next to publish</span>""").Skip(1));
        Assert.Matches(""", 00:00 UTC · in (\d+h )?\d+m</span>""", html);
    }

    [Fact]
    public async Task Page_ShowsTheAuguryRank_OnlyWhereTheScribesOverruledIt()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        var before = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        // Good and Fair anointed, ahead of the Augury's pick, which stays where the Augury put it.
        await AnointAll(good, fair);
        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.DoesNotContain("would rank it", before);
        Assert.Contains("would rank it #2", html);
        Assert.Contains("would rank it #3", html);
        Assert.Equal(2, html.Split("would rank it").Length - 1);
        Assert.Equal(2, html.Split("anointed by A Scribe (…456789)").Length - 1);
        Assert.Equal(2, html.Split("class=\"rite row scribes").Length - 1);
    }

    // The anointed come first, in a group of their own, each saying who anointed it, by the name they chose.
    [Fact]
    public async Task Page_GroupsTheAnointed_AndNamesWhoAnointedThem()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        Ok(await testbed.ScribeNaming().Choose(BacklogTestbed.Tester, "Magos Ferrum", CancellationToken.None));

        Ok(await testbed.Scribes().Anoint(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"""<article class="rite row scribes next" id="rite-{good.Id:D}" """, html);
        // Anointing places it alone: the rite it passed stays the Augury's.
        Assert.Contains("""Anointed<span class="group-count">1</span>""", html);
        Assert.Contains("""The Augury’s order<span class="group-count">1</span>""", html);
        Assert.Single(html.Split("anointed by Magos Ferrum ·").Skip(1));
    }

    // Orders saved before they kept who placed each draft still group their drafts as the Scribes'.
    [Fact]
    public async Task Page_GroupsARiteAsTheScribes_WhenTheOrderDoesntKnowWhoPlacedIt()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        Ok(await testbed.Scriptorium.SavePlacements(new Placements([good.Id], new Dictionary<Guid, string>(), LanguageExt.Prelude.None), LanguageExt.Prelude.None, [],
            AuditEntries.Of(testbed.Time, BacklogTestbed.Tester, AuditAction.Anoint, LanguageExt.Prelude.None, LanguageExt.Prelude.None), CancellationToken.None));

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"""<article class="rite row scribes next" id="rite-{good.Id:D}" """, html);
        Assert.Single(html.Split("class=\"rite row scribes").Skip(1));
        Assert.DoesNotContain("placed by ", html);
    }

    // Exalt and Humble are the anointed's alone: the Augury orders the rest. At the ends of the anointed they can't be
    // pressed, and say why.
    [Fact]
    public async Task Page_OffersExaltAndHumble_OnlyAmongTheAnointed()
    {
        var first = await testbed.AddDraft("First", 0.9f);
        var second = await testbed.AddDraft("Second", 0.8f);
        var third = await testbed.AddDraft("Third", 0.7f);
        var augurys = Html(await Function().Page(Get(""), CancellationToken.None), 200);
        await AnointAll(first, second);

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.DoesNotMatch("formaction=\"[^\"]+/(exalt|humble)\"", augurys);
        Assert.Contains("""<button class="step" title="Already next to publish" disabled>""", html);
        Assert.Contains($"""<button class="step" title="Humble: one day later" formaction="/scriptorium/drafts/{first.Id:D}/humble">""", html);
        Assert.Contains($"""<button class="step" title="Exalt: one day earlier" formaction="/scriptorium/drafts/{second.Id:D}/exalt">""", html);
        Assert.Contains("""<button class="step" title="Already the last anointed: the Augury’s order follows" disabled>""", html);
        Assert.Contains("""<span class="visually-hidden">Exalt: Second</span>""", html);
        Assert.DoesNotContain($"/scriptorium/drafts/{third.Id:D}/exalt", html);
    }

    [Fact]
    public async Task Page_GivesEachRiteItsButtons_EachIconHiddenFromScreenReaders_AndFoldsTheNotes()
    {
        var augurys = await testbed.AddDraft("Excellent", 0.95f);
        var top = await testbed.AddDraft("Good", 0.7f);
        var burned = await testbed.AddDraft("Burned", 0.3f);
        Ok(await testbed.Scribes().Burn(burned.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        // A Scribe anointed a draft, so Let the Augury Decide shows too.
        Ok(await testbed.Scribes().Anoint(top.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        // The Augury's rite is offered Anoint; the anointed one Release, in its place, and the arrows.
        Assert.Contains($""" class="primary" formaction="/scriptorium/drafts/{augurys.Id:D}/anoint">""", html);
        Assert.DoesNotContain($"/scriptorium/drafts/{top.Id:D}/anoint", html);
        Assert.Contains($""" class="secondary" formaction="/scriptorium/drafts/{top.Id:D}/release">""", html);
        Assert.DoesNotContain($"/scriptorium/drafts/{augurys.Id:D}/release", html);
        Assert.Single(html.Split("""<span class="pair" role="group" aria-label="Reorder">""").Skip(1));
        Assert.Contains($""" class="danger burn" formaction="/scriptorium/drafts/{top.Id:D}/burn">""", html);
        Assert.Contains("Let the Augury Decide", html);
        // Every button but the burn panel's reasons and its Cancel, which are words alone.
        var labels = Regex.Matches(html, """<button(?![^>]*class="(reason|cancel)")[^>]*>(.{0,120})""").Select(match => match.Groups[2].Value).ToList();
        Assert.All(labels, label => Assert.StartsWith("""<svg class="icon" aria-hidden="true" focusable="false" """, label));
        Assert.All(Regex.Matches(html, "<svg[^>]*>"), icon => Assert.Contains("""aria-hidden="true" focusable="false" """, icon.Value));
        Assert.Equal(2, html.Split("""<details class="note-fold">""").Length - 1);
        Assert.Equal(2, html.Split("""<label for="note-""").Length - 1);
        // The script folds the cards and asks why before a burn; without it, every card is open and burns at once.
        Assert.Equal(2, html.Split("""aria-expanded="true" """).Length - 1);
        Assert.Equal(2, Regex.Matches(html, """<div class="burn-panel" role="group" aria-labelledby="why-[^"]+" hidden>""").Count);
        Assert.Equal(2, html.Split("""<button type="button" class="reason" aria-pressed="false">The joke falls flat</button>""").Length - 1);
    }

    // A Scribe who acts once a month needn't remember what each of the rite's names means.
    [Fact]
    public async Task Page_SaysWhatEachActionDoes_WhereThePointerRests()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var top = await testbed.AddDraft("Good", 0.7f);
        var burned = await testbed.AddDraft("Burned", 0.3f);
        Ok(await testbed.Scribes().Burn(burned.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(top.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        var buttons = Regex.Matches(html, """<button(?![^>]*class="(reason|cancel)")[^>]*>""").Select(match => match.Value).ToList();
        Assert.All(buttons, button => Assert.Matches(""" title="[^"]{20,}"[ >]""", button));
        Assert.Contains("""<button title="Forgets the Scribes&#39; order: the Augury orders every waiting rite again, by quality and variety." class="outline">""", html);
        Assert.Contains($"""<button title="Takes this rite out of the calendar, into the Ashes. Nothing is deleted: it can be restored." class="danger burn" formaction="/scriptorium/drafts/{top.Id:D}/burn">""", html);
    }

    [Fact]
    public async Task Page_WhenCosmosFails_SaysTheScriptoriumIsSilent()
    {
        var function = new ScriptoriumFunction(
            testbed.ScribesWithoutCosmos(), testbed.Amendments(), testbed.DraftJudging(), testbed.Audit, testbed.ScribeNaming(), testbed.Tabs(), Options.Create(Settings.ScriptoriumPages), new EveryoneIsScribeSignIn(), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

        var html = Html(await function.Page(Get(""), CancellationToken.None), 500);

        Assert.Contains("The Scriptorium is silent", html);
        Assert.Contains("not affiliated with or endorsed by Games Workshop", html);
    }

    [Theory]
    [InlineData("anoint")]
    [InlineData("exalt")]
    [InlineData("humble")]
    [InlineData("release")]
    public async Task Reordering_GoesBackToThePage_AndSaysWhatHappened(string action)
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        var middle = await testbed.AddDraft("Good", 0.7f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        if (action != "anoint")
        {
            await AnointAll(excellent, middle, fair);
        }

        var response = await Function().Decide(Post("A fine note."), middle.Id, action, CancellationToken.None);

        Assert.Equal($"/scriptorium?done={action}&rite={middle.Id:D}#rite-{middle.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Null(response.RefillReason);
        var decision = Assert.Single(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions,
            found => found.Action == Enum.Parse<ScribeAction>(action, ignoreCase: true));
        Assert.Equal(LanguageExt.Prelude.Some("A fine note."), decision.Note);
    }

    [Fact]
    public async Task Burning_GoesBackToThePage_WithoutARefill_WhileDraftsStillWait()
    {
        var burned = await testbed.AddDraft("Burned", 0.5f);
        await testbed.AddDraft("Still waiting", 0.5f);

        var response = await Function().Decide(Post(""), burned.Id, "burn", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=burn&rite={burned.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Null(response.RefillReason);
    }

    [Fact]
    public async Task BurningTheLastDraft_AsksForARefillAtOnce()
    {
        var last = await testbed.AddDraft("The last one", 0.5f);

        var response = await Function().Decide(Post(""), last.Id, "burn", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=burn-last&rite={last.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Equal(ScriptoriumFunction.AllBurned, response.RefillReason);
        Assert.Empty(await testbed.Waiting());
    }

    [Fact]
    public async Task Burning_WhenTheCountAfterwardsFails_StillBurns_AndAsksForARefill()
    {
        var burned = await testbed.AddDraft("Burned", 0.5f);
        await testbed.AddDraft("Still waiting", 0.5f);
        var scribes = new Scribes(new DraftsFailingAfterFirstRead(testbed.Drafts), testbed.Rites, testbed.Scriptorium, Options.Create(Settings.Augury), Options.Create(Settings.ScriptoriumPages), Options.Create(Settings.Backlog), testbed.Time);
        var function = new ScriptoriumFunction(scribes, testbed.Amendments(), testbed.DraftJudging(), testbed.Audit, testbed.ScribeNaming(), testbed.Tabs(), Options.Create(Settings.ScriptoriumPages), new EveryoneIsScribeSignIn(), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

        var response = await function.Decide(Post(""), burned.Id, "burn", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=burn&rite={burned.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Equal(ScriptoriumFunction.Uncounted, response.RefillReason);
        Assert.Equal("Still waiting", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    [Theory]
    [InlineData("text/plain", "note=hello")]
    [InlineData("multipart/form-data; boundary=rite", "--rite\r\nnot a part")]
    public async Task AMalformedForm_IsTheClientsMistake_AndChangesNothing(string contentType, string body)
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);

        var response = await Function().Decide(Post(contentType, body), draft.Id, "burn", CancellationToken.None);

        Assert.IsType<BadRequestResult>(response.Result);
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task Restoring_GoesBackToThePage()
    {
        var draft = await testbed.AddDraft("Second chance", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var response = await Function().Decide(Post(""), draft.Id, "restore", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=restore&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task ActingOnADraftThatChanged_SaysSo_InsteadOfFailing()
    {
        var gone = Guid.NewGuid();

        var response = await Function().Decide(Post(""), gone, "burn", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=changed&rite={gone:D}#rite-{gone:D}", Assert.IsType<RedirectResult>(response.Result).Url);
    }

    [Fact]
    public async Task ExaltingTheFirst_SaysNothingMoved_RatherThanExalted()
    {
        var first = await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        await AnointAll(first, fair);

        var response = await Function().Decide(Post(""), first.Id, "exalt", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=unmoved&rite={first.Id:D}#rite-{first.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.DoesNotContain(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions, decision => decision.Action == ScribeAction.Exalt);
    }

    // A release is a judgment of its own in Recent judgments, said and drawn as one, not as a restore.
    [Fact]
    public async Task RecentJudgments_ShowARelease_AsARelease()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        await AnointAll(fair);
        Ok(await testbed.Scribes().Release(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<span class="verb">Released to the Augury:</span> <cite>Fair</cite>""", html);
        Assert.Contains($"""<span class="judgment-icon">{ScriptoriumIcons.Release}</span>""", html);
    }

    // Exalting a rite the Augury orders comes from a page shown before it was released: the calendar changed.
    [Fact]
    public async Task ExaltingARiteTheAuguryOrders_SaysTheCalendarChanged()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);

        var response = await Function().Decide(Post(""), fair.Id, "exalt", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=changed&rite={fair.Id:D}#rite-{fair.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Empty(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions);
    }

    [Fact]
    public async Task ANoteTooLong_SaysSo_AndChangesNothing()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);

        var response = await Function().Decide(
            Post(new string('a', ScribeDecision.MaxNoteLength + 1)), draft.Id, "humble", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=note-too-long&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
    }

    [Fact]
    public async Task AnUnknownAction_DoesNotExist()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);

        var response = await Function().Decide(Post(""), draft.Id, "smite", CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task AFormPostedFromAnotherSite_IsForbidden_AndChangesNothing()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);
        var forged = Post("");
        forged.Headers["Sec-Fetch-Site"] = "cross-site";

        var response = await Function().Decide(forged, draft.Id, "burn", CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(response.Result).StatusCode);
        Assert.Single(await testbed.Waiting());
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://elsewhere.example")]
    // This site's name over plain HTTP is another origin, which anyone on the reader's network can put a page on.
    [InlineData("http://dailymachinespirit.fyi")]
    public async Task AFormWithoutFetchMetadata_IsForbidden_UnlessItsOriginIsThisSite(string origin)
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);
        var post = Post("");
        post.Headers.Remove("Sec-Fetch-Site");
        post.Headers.Origin = origin;

        var response = await Function().Decide(post, draft.Id, "burn", CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(response.Result).StatusCode);
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task AFormWithoutFetchMetadata_FromThisSite_IsAccepted()
    {
        var draft = await testbed.AddDraft("Burned", 0.5f);
        var post = Post("");
        post.Headers.Remove("Sec-Fetch-Site");
        post.Headers.Origin = "https://dailymachinespirit.fyi";

        var response = await Function().Decide(post, draft.Id, "burn", CancellationToken.None);

        Assert.IsType<RedirectResult>(response.Result);
        Assert.Empty(await testbed.Waiting());
    }

    [Fact]
    public async Task SomeoneWhoIsNotAScribe_IsForbidden_EverywhereInTheScriptorium_AndChangesNothing()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);
        var function = new ScriptoriumFunction(
            testbed.Scribes(), testbed.Amendments(), testbed.DraftJudging(), testbed.Audit, testbed.ScribeNaming(), testbed.Tabs(), Options.Create(Settings.ScriptoriumPages), new NobodyIsScribeSignIn(), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

        var page = await function.Page(Get(""), CancellationToken.None);
        var decision = await function.Decide(Post(""), draft.Id, "burn", CancellationToken.None);
        var summon = await function.Summon(Post(""), CancellationToken.None);
        var augury = await function.LetTheAuguryDecide(Post(""), CancellationToken.None);
        var ashes = await function.Ashes(Get(""), CancellationToken.None);

        Assert.All([page, decision.Result, summon.Result, augury, ashes],
            result => Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode));
        Assert.Null(summon.RefillReason);
        Assert.Single(await testbed.Waiting());
        Assert.Empty(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions);
    }

    [Fact]
    public async Task Summon_AsksForARefill_AndGoesBackToThePage()
    {
        var response = await Function().Summon(Post(""), CancellationToken.None);

        Assert.Equal("/scriptorium?done=summon", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Equal(ScriptoriumFunction.Summoned, response.RefillReason);
    }

    [Fact]
    public async Task LetTheAuguryDecide_ForgetsTheScribesOrder_AndGoesBackToThePage()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var result = await Function().LetTheAuguryDecide(Post(""), CancellationToken.None);

        Assert.Equal("/scriptorium?done=augury", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("Excellent", Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0].Draft.Writing.Title);
    }

    [Fact]
    public async Task TheCalendar_OffersToAmendEachWaitingDraft()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"""href="/scriptorium/drafts/{draft.Id:D}/amend">""", html);
    }

    // The draft's words in a plain form. Its script only says a rewrite is under way, and asks for it in the background;
    // asking needs a note.
    [Fact]
    public async Task AmendPage_ShowsTheDraftsWords_InAPlainForm()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var html = Html(await Function().AmendPage(Get(""), draft.Id, CancellationToken.None), 200);

        Assert.Contains("""<input type="text" id="amend-title" name="title" value="Litany of the Cold Cache" """, html);
        Assert.Contains("""<input type="hidden" name="etag" value=""", html);
        Assert.Contains($"""formaction="/scriptorium/drafts/{draft.Id:D}/rewrite" formnovalidate data-needs-note""", html);
        Assert.Contains(SlowActionScript.Js, html);
        Assert.DoesNotContain(ScriptoriumScript.Js, html);
        Assert.Contains("""data-needs-note data-in-place data-slow="The Machine Spirit is rewriting it… usually a few seconds, at most a minute and a half." data-busy-label="Rewriting…">""", html);
        Assert.Contains("Needed to ask for a rewrite, which follows it", html);
        Assert.DoesNotContain("""<span class="changed-mark">""", html);
    }

    [Fact]
    public async Task AmendPage_ForADraftNoLongerWaiting_GoesBackToTheCalendar()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var result = await Function().AmendPage(Get(""), draft.Id, CancellationToken.None);

        Assert.Equal($"/scriptorium?done=changed&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(result).Url);
    }

    // Only a draft judging couldn't score offers it: judging a judged one again would change nothing its words did.
    [Fact]
    public async Task JudgeAgain_IsOfferedOnlyForAnUnjudgedDraft_AndJudgesIt()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        var judged = await testbed.AddDraft("Already judged", 0.9f);
        var page = Html(await Function().Page(Get(""), CancellationToken.None), 200);
        Assert.Contains($"""formaction="/scriptorium/drafts/{unjudged.Id:D}/judge">""", page);
        Assert.DoesNotContain($"/scriptorium/drafts/{judged.Id:D}/judge", page);

        var response = await Function().Decide(Post(""), unjudged.Id, "judge", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=judged&rite={unjudged.Id:D}#rite-{unjudged.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        var html = Html(await Function().Page(Following(response.Result), CancellationToken.None), 200);
        Assert.Matches("Judged “Litany of the Cold Cache” again, \\d+%: the Augury places it for ", html);
        Assert.Contains($"""A Scribe on staging judged <a href="/scriptorium/record?draft={unjudged.Id:D}">“Litany of the Cold Cache”</a> again""", ScriptoriumPage.RenderRecord(
            Ok(await testbed.Audit.GetHistory(unjudged.Id, CancellationToken.None)), [], LanguageExt.Prelude.None, LanguageExt.Prelude.None, LanguageExt.Prelude.None));
    }

    [Fact]
    public async Task JudgeAgain_SaysWhenItStillCant_OrHasNothingToJudge()
    {
        var unjudged = await testbed.AddUnjudgedDraft("Litany of the Cold Cache");
        var judged = await testbed.AddDraft("Already judged", 0.9f);
        testbed.QualityJudgesFail(Settings.QualityJudging.ScoresPerDraft);

        var still = await Function().Decide(Post(""), unjudged.Id, "judge", CancellationToken.None);
        var nothing = await Function().Decide(Post(""), judged.Id, "judge", CancellationToken.None);

        Assert.StartsWith("/scriptorium?done=still-unjudged", Assert.IsType<RedirectResult>(still.Result).Url);
        Assert.Contains("Still “Litany of the Cold Cache” can't be judged",
            Html(await Function().Page(Following(still.Result), CancellationToken.None), 200));
        Assert.StartsWith("/scriptorium?done=changed", Assert.IsType<RedirectResult>(nothing.Result).Url);
    }

    // A textarea's maxlength counts a line break as one character, but the browser posts it as two (CRLF): words that
    // fit the field fit here too.
    [Fact]
    public async Task Amend_TakesMultilineWords_UpToTheFieldsLimits()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        var text = Lines(RiteWriting.MaxTextLength);
        var truth = Lines(RiteWriting.MaxHereticalTruthLength);
        var ailment = Lines(RiteWriting.MaxAilmentLength);
        var form = string.Join("&", new[] { ("etag", await ETag(draft)), ("title", "The Long Litany"), ("text", text), ("truth", truth), ("ailment", ailment), ("note", "") }
            .Select(field => $"{field.Item1}={Uri.EscapeDataString(field.Item2)}"));

        var response = await Function().Decide(Post("application/x-www-form-urlencoded", form), draft.Id, "amend", CancellationToken.None);

        Assert.StartsWith("/scriptorium?done=amend", Assert.IsType<RedirectResult>(response.Result).Url);
        var saved = Assert.Single(await testbed.Waiting());
        Assert.Equal((text.Replace("\r\n", "\n"), truth.Replace("\r\n", "\n")), (saved.Writing.Text, saved.Writing.HereticalTruth));

        // As a textarea counts it: each line break one character, the whole at the limit.
        static string Lines(int limit)
        {
            var lines = Enumerable.Repeat(new string('O', 99), limit / 100).ToList();
            var counted = lines.Sum(line => line.Length) + lines.Count - 1;
            lines[^1] += new string('O', limit - counted);
            return string.Join("\r\n", lines);
        }
    }

    [Fact]
    public async Task Amend_SavesTheWords_AndTheCalendarSaysSo()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var response = await Function().Decide(
            Amending(await ETag(draft), "The Litany of the Warm Cache", "Sharper."), draft.Id, "amend", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=amend&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        var html = Html(await Function().Page(Following(response.Result), CancellationToken.None), 200);
        Assert.Contains("Amended “The Litany of the Warm Cache”: judged again, it waits for Wed 7 Oct.", html);
        Assert.Contains("""written by <span class="nowrap">gpt-6-sol</span>, amended by a Scribe""", html);
        Assert.Contains("""<span class="verb">Amended:</span> <cite>Litany of the Cold Cache</cite>""", html);
    }

    // Unjudged, a draft the Scribes placed keeps its place, even first: the message says when it goes out.
    [Fact]
    public async Task Amend_Unjudged_SaysWhenItGoesOut()
    {
        await testbed.AddDraft("Best", 0.9f);
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        Ok(await testbed.Scribes().Anoint(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        testbed.QualityJudgesFail(Settings.QualityJudging.ScoresPerDraft);

        var response = await Function().Decide(
            Amending(await ETag(draft), "The Litany of the Warm Cache", "Sharper."), draft.Id, "amend", CancellationToken.None);

        var html = Html(await Function().Page(Following(response.Result), CancellationToken.None), 200);
        Assert.Contains("Amended “The Litany of the Warm Cache”, but it couldn't be judged again, so it waits unjudged for Wed 7 Oct.", html);
    }

    // Refused, the page comes back with what the Scribe typed and why: nothing they wrote is lost.
    [Fact]
    public async Task Amend_WordsBreakingARule_ComeBackWithWhy_KeepingTheScribesWords()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var response = await Function().Decide(
            Amending(await ETag(draft), "The Warhammer Litany", "Sharper."), draft.Id, "amend", CancellationToken.None);

        var html = Html(response.Result, 422);
        Assert.Contains("The answer uses forbidden names: Warhammer.", html);
        Assert.Contains("""value="The Warhammer Litany" """, html);
        Assert.Contains("Sharper.</textarea>", html);
        Assert.Equal("Litany of the Cold Cache", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    // The rewrite fills the form, unsaved: the Scribe reads it before saving it.
    [Fact]
    public async Task Rewrite_ComesBackWithTheRewrite_Unsaved()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        testbed.Models.Answers(BacklogTestbed.Sol, BacklogTestbed.Answer("The Litany of the Warm Cache"));

        var response = await Function().Decide(
            Amending(await ETag(draft), "Litany of the Cold Cache", "Warm it up."), draft.Id, "rewrite", CancellationToken.None);

        var html = Html(response.Result, 200);
        Assert.Contains("The Machine Spirit rewrote it as your note asks.", html);
        Assert.Contains("""value="The Litany of the Warm Cache" """, html);
        Assert.Equal("Litany of the Cold Cache", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    // What the rewrite changed says so in its label, and the first such field is focused; what it kept doesn't.
    [Fact]
    public async Task Rewrite_MarksTheFieldsItChanged()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        testbed.Models.Answers(BacklogTestbed.Sol,
            FakeChatClients.Answer("Litany of the Cold Cache", "O Machine Spirit, let thy cache be warm.", "A warm cache is one that was read before."));

        var response = await Function().Decide(
            Amending(await ETag(draft), "Litany of the Cold Cache", "Warm it up."), draft.Id, "rewrite", CancellationToken.None);

        var html = Html(response.Result, 200);
        Assert.Contains("""<label for="amend-text">Text<span class="changed-mark"> · rewritten</span></label>""", html);
        Assert.Contains("""<textarea id="amend-text" name="text" rows="6" maxlength="2000" class="changed" autofocus required>""", html);
        Assert.Contains("""<label for="amend-title">Title</label>""", html);
        Assert.Contains("""<label for="amend-truth">Heretical Truth</label>""", html);
        // A rewrite can supply an ailment an older draft lacks: the empty required field mustn't block asking for one.
        Assert.Contains("""formaction="/scriptorium/drafts/""", html);
        Assert.Matches("formaction=\"/scriptorium/drafts/[^\"]+/rewrite\" formnovalidate", html);
        Assert.Contains("""<label for="amend-ailment">Ailment<span class="changed-mark"> · rewritten</span></label>""", html);
        Assert.Single(Regex.Matches(html, "class=\"changed\" autofocus"));
    }

    [Fact]
    public async Task Rewrite_WhenNoModelAnswers_ComesBackSayingSo_KeepingTheScribesWords()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        testbed.Models.Fails(BacklogTestbed.Sol, 3).Fails(BacklogTestbed.Luna, 3);

        var response = await Function().Decide(
            Amending(await ETag(draft), "Litany of the Cold Cache, Amended", "Warm it up."), draft.Id, "rewrite", CancellationToken.None);

        var html = Html(response.Result, 503);
        Assert.Contains("The Machine Spirit couldn&#39;t rewrite it just now.", html);
        Assert.Contains("""value="Litany of the Cold Cache, Amended" """, html);
    }

    // A summon changes nothing stored itself, so its entry is saved alone, naming who summoned.
    [Fact]
    public async Task Summon_IsRecorded()
    {
        await Function().Summon(Post(""), CancellationToken.None);

        var entry = Assert.Single(Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)));
        Assert.Equal((AuditEntry.StagingScribe, AuditAction.Summon), (entry.Actor, entry.Action));
    }

    [Fact]
    public async Task TheRecord_ListsEachChange_AndWhoMadeIt_AllOrOneRites()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        await testbed.AddDraft("The Rite of Re-Run", 0.5f);
        await Function().Decide(Post(""), draft.Id, "burn", CancellationToken.None);

        var all = Html(await Function().Record(Get(""), CancellationToken.None), 200);
        var one = Html(await Function().Record(Get($"?draft={draft.Id:D}"), CancellationToken.None), 200);

        var rite = $"""<a href="/scriptorium/record?draft={draft.Id:D}">“Litany of the Cold Cache”</a>""";
        Assert.Contains($"""A Scribe on staging consigned {rite} to the flames <span class="when">(from #2)</span>""", all);
        Assert.Contains($"The Machine Spirit wrote {rite}", all);
        Assert.Contains("“The Rite of Re-Run”", all);
        Assert.Contains("One rite's record.", one);
        Assert.DoesNotContain("“The Rite of Re-Run”", one);
        var calendar = Html(await Function().Page(Get(""), CancellationToken.None), 200);
        Assert.Contains("""<a href="/scriptorium/record">The Record<span class="count">""", calendar);
    }

    [Fact]
    public async Task TheRecord_IsTheScribesAlone()
    {
        var notAScribe = new ScriptoriumFunction(testbed.Scribes(), testbed.Amendments(), testbed.DraftJudging(), testbed.Audit, testbed.ScribeNaming(), testbed.Tabs(), Options.Create(Settings.ScriptoriumPages), new NobodyIsScribeSignIn(), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await notAScribe.Record(Get(""), CancellationToken.None)).StatusCode);
    }

    private ScriptoriumFunction Function()
        => new(testbed.Scribes(), testbed.Amendments(), testbed.DraftJudging(), testbed.Audit, testbed.ScribeNaming(), testbed.Tabs(), Options.Create(Settings.ScriptoriumPages), new EveryoneIsScribeSignIn(), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

    private static HttpRequest Get(string query)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "GET";
        if (query.Length > 0)
        {
            request.QueryString = new QueryString(query);
        }
        return request;
    }

    private async Task AnointAll(params Draft[] drafts)
    {
        foreach (var draft in drafts)
        {
            Ok(await testbed.Scribes().Anoint(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        }
    }

    // Where a redirect leads, as the browser asks for it: the address without its #fragment.
    private static HttpRequest Following(IActionResult redirect)
        => Get(new Uri(new Uri("https://dailymachinespirit.fyi"), Assert.IsType<RedirectResult>(redirect).Url).Query);

    // As a browser posts the page's form: same origin, with the note field, url-encoded.
    private static HttpRequest Post(string note)
        => Post("application/x-www-form-urlencoded", $"note={Uri.EscapeDataString(note)}");

    // As the amending page posts its form: the version read, the words, and the note.
    private static HttpRequest Amending(string etag, string title, string note)
        => Post("application/x-www-form-urlencoded", string.Join("&", new[]
        {
            ("etag", etag),
            ("title", title),
            ("text", "O Machine Spirit, warm thy cache."),
            ("truth", "A warm cache is one that was read before."),
            ("ailment", "The build is slow after the cache is cleared."),
            ("note", note),
        }.Select(field => $"{field.Item1}={Uri.EscapeDataString(field.Item2)}")));

    private async Task<string> ETag(Draft draft)
        => Ok(await testbed.Amendments().FindWaiting(draft.Id, CancellationToken.None))
            .Map(amendable => amendable.ETag)
            .IfNone(() => throw new Xunit.Sdk.XunitException("The draft isn't amendable."));

    private static HttpRequest Post(string contentType, string body)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "POST";
        request.Host = new HostString("dailymachinespirit.fyi");
        request.Headers["Sec-Fetch-Site"] = "same-origin";
        request.ContentType = contentType;
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return request;
    }

    private static string Html(IActionResult result, int status)
    {
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(status, content.StatusCode);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        return content.Content ?? string.Empty;
    }
}
