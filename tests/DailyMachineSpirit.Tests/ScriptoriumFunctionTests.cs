using DailyMachineSpirit.Data.Entities;
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
    public async Task Page_ShowsTheCalendar_TheAshes_AndTheJudgments()
    {
        await testbed.AddDraft("The Rite of `git push --force`", 0.9f);
        var burned = await testbed.AddDraft("Litany of the Endless Spinner", 0.3f, RiteKind.Ritual);
        Ok(await testbed.Scribes().Burn(burned.Id, "Spinners <again>.", BacklogTestbed.Tester, CancellationToken.None));
        var request = Get("");

        var html = Html(await Function(enabled: true).Page(request, CancellationToken.None), 200);

        Assert.Contains("The Liturgical Calendar", html);
        Assert.Contains("The Rite of <code>git push --force</code>", html);
        Assert.Contains("Litany of the Endless Spinner", html);
        Assert.Contains("Restore from the Ashes", html);
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

    // So the Scribes see what the calendar covers before it's published.
    [Fact]
    public async Task Page_ShowsWhatEachDraftIsAbout_AndTheCalendarsSubjects()
    {
        await testbed.AddDraft("The Rite of the Hard Refresh", 0.9f, RiteKind.Ritual, new[] { "caches" });
        await testbed.AddDraft("The Litany of Forgotten Names", 0.8f, RiteKind.Prayer, new[] { "infrastructure", "caches" });
        await testbed.AddDraft("The Unjudged Rite", 0.7f);

        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("Subjects waiting: Caches (2), Infrastructure (1), untagged (1).", html);
        Assert.Contains("""Forgotten Names</h3><p class="tags"><span class="visually-hidden">Filed under </span><span class="tag">Infrastructure</span><span class="tag">Caches</span></p>""", html);
        Assert.DoesNotContain("filed under Infrastructure", html);
    }

    [Fact]
    public async Task Page_WithAnEmptyBacklog_SaysSo_AndOffersToSummon()
    {
        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("No rite waits", html);
        Assert.Contains("Summon New Rites", html);
        Assert.Contains("Nothing has been consigned to the flames.", html);
        Assert.DoesNotContain("Let the Augury Decide", html);
    }

    [Fact]
    public async Task Page_CarriesTheSitesIcon_Inline()
    {
        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<link rel="icon" type="image/svg+xml" href="data:image/svg+xml,%3Csvg""", html);
    }

    [Fact]
    public async Task Page_UsesTheSitesOwnFonts_AndContactsNoOtherSite()
    {
        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("url(/fonts/cinzel.woff2)", html);
        Assert.DoesNotContain("https://", html);
    }

    [Fact]
    public async Task Page_SaysWhatTheLastActionDid()
    {
        var html = Html(await Function(enabled: true).Page(Get("?done=burn"), CancellationToken.None), 200);

        Assert.Contains("Consigned to the flames.", html);
    }

    [Theory]
    [InlineData("anoint", "Anointed: it goes out next.")]
    [InlineData("exalt", "Exalted.")]
    [InlineData("humble", "Humbled.")]
    [InlineData("restore", "Restored from the ashes.")]
    [InlineData("burn-last", "Consigned to the flames. No rite waits now")]
    [InlineData("summon", "New rites are being summoned")]
    [InlineData("augury", "The Augury orders every draft again.")]
    [InlineData("changed", "Another Scribe changed the calendar meanwhile.")]
    [InlineData("unmoved", "It was already there, so nothing moved.")]
    [InlineData("note-too-long", "That note is too long, so nothing was changed.")]
    public async Task Page_SaysWhatTheLastActionDid_EvenWhenTheRiteIsGoneSince(string done, string message)
    {
        var html = Html(await Function(enabled: true).Page(Get($"?done={done}&rite={Guid.NewGuid():D}"), CancellationToken.None), 200);

        Assert.Contains($"""<p class="status" role="status">{message}""", html);
    }

    [Fact]
    public async Task Page_SaysNothing_ForAnUnknownOutcome()
    {
        var html = Html(await Function(enabled: true).Page(Get("?done=smite"), CancellationToken.None), 200);

        Assert.Contains("""<p class="status" role="status"></p>""", html);
    }

    [Theory]
    [InlineData("exalt", "Exalted")]
    [InlineData("anoint", "Anointed")]
    public async Task AfterAMove_ThePageNamesTheRite_AndWhenItNowGoesOut(string action, string done)
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        var function = Function(enabled: true);

        var response = await function.Decide(Post(""), good.Id, action, CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);

        var first = Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0];
        Assert.Equal(good.Id, first.Draft.Id);
        Assert.Contains($"{done} “Good”", html);
        Assert.Contains(first.PublishesOnUtc.ToString("ddd d MMM", CultureInfo.InvariantCulture) + ".", html);
        Assert.Contains($"""<article class="rite acted" id="rite-{good.Id:D}" """, html);
    }

    [Fact]
    public async Task AfterAHumbling_ThePageSaysWhenTheRiteNowGoesOut()
    {
        var excellent = await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Good", 0.7f);
        var function = Function(enabled: true);

        var response = await function.Decide(Post(""), excellent.Id, "humble", CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);

        var second = Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[1];
        Assert.Equal(excellent.Id, second.Draft.Id);
        Assert.Contains($"Humbled “Excellent” to {second.PublishesOnUtc.ToString("ddd d MMM", CultureInfo.InvariantCulture)}.", html);
    }

    [Fact]
    public async Task AfterABurn_TheMessageLinksToTheRiteInTheAshes()
    {
        var burned = await testbed.AddDraft("Burned", 0.5f);
        await testbed.AddDraft("Still waiting", 0.5f);
        var function = Function(enabled: true);

        var response = await function.Decide(Post(""), burned.Id, "burn", CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);

        Assert.Contains($"""Consigned “Burned” to the flames. <a href="#rite-{burned.Id:D}">Restore it from the Ashes</a>.""", html);
        Assert.Contains($"""<article class="rite ash acted" id="rite-{burned.Id:D}" """, html);
    }

    // So the Scribe's eye finds where it went, with or without the script that slides it there.
    [Fact]
    public async Task AfterAnAction_TheRiteItWasOnIsMarked_AndNoOther()
    {
        await testbed.AddDraft("First", 0.9f);
        var exalted = await testbed.AddDraft("Second", 0.5f);
        var function = Function(enabled: true);

        var response = await function.Decide(Post(""), exalted.Id, "exalt", CancellationToken.None);
        var html = Html(await function.Page(Following(response.Result), CancellationToken.None), 200);
        var before = Html(await function.Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"""<article class="rite acted" id="rite-{exalted.Id:D}" """, html);
        Assert.Single(html.Split("class=\"rite acted\"").Skip(1));
        Assert.DoesNotContain("class=\"rite acted\"", before);
    }

    // The forms work without it; with it, an action is shown in place.
    [Fact]
    public async Task Page_CarriesTheScriptThatShowsActionsInPlace()
    {
        await testbed.AddDraft("First", 0.9f);

        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"<script>{ScriptoriumScript.Js}</script>", html);
    }

    [Fact]
    public async Task AfterARestore_ThePageSaysWhenTheRiteGoesOut()
    {
        var draft = await testbed.AddDraft("Second chance", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var function = Function(enabled: true);

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
        var due = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);
        Ok(await testbed.Publisher().PublishToday(CancellationToken.None));
        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("Publishes next", due);
        Assert.DoesNotContain("00:00 UTC", due);
        Assert.Single(html.Split("Publishes at 00:00 UTC").Skip(1));
    }

    [Fact]
    public async Task Page_ShowsTheAuguryPlace_OnlyWhereTheScribesOverruledIt()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var good = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);
        var before = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        // Good goes up and Excellent down, both placed by the Scribes; Fair stays where the Augury put it.
        Ok(await testbed.Scribes().Exalt(good.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.DoesNotContain("it would place it", before);
        Assert.Contains("it would place it #2", html);
        Assert.Contains("it would place it #1", html);
        Assert.Equal(2, html.Split("it would place it").Length - 1);
        Assert.Equal(2, html.Split("Placed by the Scribes").Length - 1);
    }

    [Fact]
    public async Task Page_GroupsTheButtons_EachWithAnIconHiddenFromScreenReaders_AndFoldsTheNotes()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var top = await testbed.AddDraft("Good", 0.7f);
        var burned = await testbed.AddDraft("Burned", 0.3f);
        Ok(await testbed.Scribes().Burn(burned.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        // A Scribe placed a draft, so Let the Augury Decide shows too.
        Ok(await testbed.Scribes().Anoint(top.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($""" class="primary" formaction="/scriptorium/drafts/{top.Id:D}/anoint">""", html);
        Assert.Contains("""<span class="pair" role="group" aria-label="Reorder">""", html);
        Assert.Contains($""" class="danger burn" formaction="/scriptorium/drafts/{top.Id:D}/burn">""", html);
        Assert.Contains("Let the Augury Decide", html);
        var buttons = Regex.Matches(html, "<button[^>]*>(.{0,120})").Select(match => match.Groups[1].Value).ToList();
        Assert.Equal(9, buttons.Count);
        Assert.All(buttons, label => Assert.StartsWith("""<svg class="icon" aria-hidden="true" focusable="false" """, label));
        Assert.All(Regex.Matches(html, "<svg[^>]*>"), icon => Assert.Contains("""aria-hidden="true" focusable="false" """, icon.Value));
        Assert.Equal(3, html.Split("""<details class="fold">""").Length - 1);
        Assert.Equal(3, html.Split("""<label class="visually-hidden" for="note-""").Length - 1);
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

        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        var buttons = Regex.Matches(html, "<button[^>]*>").Select(match => match.Value).ToList();
        Assert.Equal(9, buttons.Count);
        Assert.All(buttons, button => Assert.Matches("""^<button title="[^"]{20,}" """, button));
        Assert.Contains("""<button title="Forgets the Scribes&#39; order: the Augury orders every waiting rite again, by quality and variety." class="outline">""", html);
        Assert.Contains($"""<button title="Takes this rite out of the calendar, into the Ashes. Nothing is deleted: it can be restored." class="danger burn" formaction="/scriptorium/drafts/{top.Id:D}/burn">""", html);
    }

    [Fact]
    public async Task Page_WhenTurnedOff_DoesNotExist()
    {
        Assert.IsType<NotFoundResult>(await Function(enabled: false).Page(Get(""), CancellationToken.None));
    }

    [Fact]
    public async Task Page_WhenCosmosFails_SaysTheScriptoriumIsSilent()
    {
        var function = new ScriptoriumFunction(
            testbed.ScribesWithoutCosmos(), testbed.Amendments(), testbed.Audit, new EveryoneIsScribeSignIn(), Options.Create(new ScriptoriumOptions { Enabled = true }), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

        var html = Html(await function.Page(Get(""), CancellationToken.None), 500);

        Assert.Contains("The Scriptorium is silent", html);
        Assert.Contains("not affiliated with or endorsed by Games Workshop", html);
    }

    [Theory]
    [InlineData("anoint")]
    [InlineData("exalt")]
    [InlineData("humble")]
    public async Task Reordering_GoesBackToThePage_AndSaysWhatHappened(string action)
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var middle = await testbed.AddDraft("Good", 0.7f);
        await testbed.AddDraft("Fair", 0.4f);

        var response = await Function(enabled: true).Decide(Post("A fine note."), middle.Id, action, CancellationToken.None);

        Assert.Equal($"/scriptorium?done={action}&rite={middle.Id:D}#rite-{middle.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Null(response.RefillReason);
        var decision = Assert.Single(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions);
        Assert.Equal(LanguageExt.Prelude.Some("A fine note."), decision.Note);
    }

    [Fact]
    public async Task Burning_GoesBackToThePage_WithoutARefill_WhileDraftsStillWait()
    {
        var burned = await testbed.AddDraft("Burned", 0.5f);
        await testbed.AddDraft("Still waiting", 0.5f);

        var response = await Function(enabled: true).Decide(Post(""), burned.Id, "burn", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=burn&rite={burned.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Null(response.RefillReason);
    }

    [Fact]
    public async Task BurningTheLastDraft_AsksForARefillAtOnce()
    {
        var last = await testbed.AddDraft("The last one", 0.5f);

        var response = await Function(enabled: true).Decide(Post(""), last.Id, "burn", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=burn-last&rite={last.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Equal(ScriptoriumFunction.AllBurned, response.RefillReason);
        Assert.Empty(await testbed.Waiting());
    }

    [Fact]
    public async Task Burning_WhenTheCountAfterwardsFails_StillBurns_AndAsksForARefill()
    {
        var burned = await testbed.AddDraft("Burned", 0.5f);
        await testbed.AddDraft("Still waiting", 0.5f);
        var scribes = new Scribes(new DraftsFailingAfterFirstRead(testbed.Drafts), testbed.Rites, testbed.Scriptorium, testbed.Time);
        var function = new ScriptoriumFunction(scribes, testbed.Amendments(), testbed.Audit, new EveryoneIsScribeSignIn(), Options.Create(new ScriptoriumOptions { Enabled = true }), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

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

        var response = await Function(enabled: true).Decide(Post(contentType, body), draft.Id, "burn", CancellationToken.None);

        Assert.IsType<BadRequestResult>(response.Result);
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task Restoring_GoesBackToThePage()
    {
        var draft = await testbed.AddDraft("Second chance", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var response = await Function(enabled: true).Decide(Post(""), draft.Id, "restore", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=restore&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task ActingOnADraftThatChanged_SaysSo_InsteadOfFailing()
    {
        var gone = Guid.NewGuid();

        var response = await Function(enabled: true).Decide(Post(""), gone, "burn", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=changed&rite={gone:D}#rite-{gone:D}", Assert.IsType<RedirectResult>(response.Result).Url);
    }

    [Fact]
    public async Task ExaltingTheFirst_SaysNothingMoved_RatherThanExalted()
    {
        var first = await testbed.AddDraft("Excellent", 0.95f);
        await testbed.AddDraft("Fair", 0.4f);

        var response = await Function(enabled: true).Decide(Post(""), first.Id, "exalt", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=unmoved&rite={first.Id:D}#rite-{first.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Empty(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions);
    }

    [Fact]
    public async Task ANoteTooLong_SaysSo_AndChangesNothing()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);

        var response = await Function(enabled: true).Decide(
            Post(new string('a', ScribeDecision.MaxNoteLength + 1)), draft.Id, "humble", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=note-too-long&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
    }

    [Fact]
    public async Task AnUnknownAction_DoesNotExist()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);

        var response = await Function(enabled: true).Decide(Post(""), draft.Id, "smite", CancellationToken.None);

        Assert.IsType<NotFoundResult>(response.Result);
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task AFormPostedFromAnotherSite_IsForbidden_AndChangesNothing()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);
        var forged = Post("");
        forged.Headers["Sec-Fetch-Site"] = "cross-site";

        var response = await Function(enabled: true).Decide(forged, draft.Id, "burn", CancellationToken.None);

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

        var response = await Function(enabled: true).Decide(post, draft.Id, "burn", CancellationToken.None);

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

        var response = await Function(enabled: true).Decide(post, draft.Id, "burn", CancellationToken.None);

        Assert.IsType<RedirectResult>(response.Result);
        Assert.Empty(await testbed.Waiting());
    }

    [Fact]
    public async Task Actions_WhenTurnedOff_DoNotExist()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);
        var function = Function(enabled: false);

        Assert.IsType<NotFoundResult>((await function.Decide(Post(""), draft.Id, "burn", CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await function.Summon(Post(""), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>(await function.LetTheAuguryDecide(Post(""), CancellationToken.None));
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task SomeoneWhoIsNotAScribe_IsForbidden_EverywhereInTheScriptorium_AndChangesNothing()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);
        var function = new ScriptoriumFunction(
            testbed.Scribes(), testbed.Amendments(), testbed.Audit, new NobodyIsScribeSignIn(), Options.Create(new ScriptoriumOptions { Enabled = true }), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

        var page = await function.Page(Get(""), CancellationToken.None);
        var decision = await function.Decide(Post(""), draft.Id, "burn", CancellationToken.None);
        var summon = await function.Summon(Post(""), CancellationToken.None);
        var augury = await function.LetTheAuguryDecide(Post(""), CancellationToken.None);

        Assert.All([page, decision.Result, summon.Result, augury],
            result => Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode));
        Assert.Null(summon.RefillReason);
        Assert.Single(await testbed.Waiting());
        Assert.Empty(Ok(await testbed.Scribes().View(CancellationToken.None)).Decisions);
    }

    [Fact]
    public async Task Summon_AsksForARefill_AndGoesBackToThePage()
    {
        var response = await Function(enabled: true).Summon(Post(""), CancellationToken.None);

        Assert.Equal("/scriptorium?done=summon", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Equal(ScriptoriumFunction.Summoned, response.RefillReason);
    }

    [Fact]
    public async Task LetTheAuguryDecide_ForgetsTheScribesOrder_AndGoesBackToThePage()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var result = await Function(enabled: true).LetTheAuguryDecide(Post(""), CancellationToken.None);

        Assert.Equal("/scriptorium?done=augury", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("Excellent", Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0].Draft.Writing.Title);
    }

    [Fact]
    public async Task TheCalendar_OffersToAmendEachWaitingDraft()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"""href="/scriptorium/drafts/{draft.Id:D}/amend">""", html);
    }

    // The draft's words in a plain form, which posts without the calendar's script.
    [Fact]
    public async Task AmendPage_ShowsTheDraftsWords_InAPlainForm()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var html = Html(await Function(enabled: true).AmendPage(Get(""), draft.Id, CancellationToken.None), 200);

        Assert.Contains("""<input type="text" id="amend-title" name="title" value="Litany of the Cold Cache" """, html);
        Assert.Contains("""<input type="hidden" name="etag" value=""", html);
        Assert.Contains($"""formaction="/scriptorium/drafts/{draft.Id:D}/rewrite">""", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public async Task AmendPage_ForADraftNoLongerWaiting_GoesBackToTheCalendar()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));

        var result = await Function(enabled: true).AmendPage(Get(""), draft.Id, CancellationToken.None);

        Assert.Equal($"/scriptorium?done=changed&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public async Task Amend_SavesTheWords_AndTheCalendarSaysSo()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var response = await Function(enabled: true).Decide(
            Amending(await ETag(draft), "The Litany of the Warm Cache", "Sharper."), draft.Id, "amend", CancellationToken.None);

        Assert.Equal($"/scriptorium?done=amend&rite={draft.Id:D}#rite-{draft.Id:D}", Assert.IsType<RedirectResult>(response.Result).Url);
        var html = Html(await Function(enabled: true).Page(Following(response.Result), CancellationToken.None), 200);
        Assert.Contains("Amended “The Litany of the Warm Cache”: judged again, it waits for Wed 7 Oct.", html);
        Assert.Contains("""written by <span class="nowrap">gpt-6-sol</span>, amended by a Scribe""", html);
        Assert.Contains("Amended: <cite>Litany of the Cold Cache</cite>", html);
    }

    // Refused, the page comes back with what the Scribe typed and why: nothing they wrote is lost.
    [Fact]
    public async Task Amend_WordsBreakingARule_ComeBackWithWhy_KeepingTheScribesWords()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);

        var response = await Function(enabled: true).Decide(
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

        var response = await Function(enabled: true).Decide(
            Amending(await ETag(draft), "Litany of the Cold Cache", "Warm it up."), draft.Id, "rewrite", CancellationToken.None);

        var html = Html(response.Result, 200);
        Assert.Contains("The Machine Spirit rewrote it as your note asks.", html);
        Assert.Contains("""value="The Litany of the Warm Cache" """, html);
        Assert.Equal("Litany of the Cold Cache", Assert.Single(await testbed.Waiting()).Writing.Title);
    }

    [Fact]
    public async Task Rewrite_WhenNoModelAnswers_ComesBackSayingSo_KeepingTheScribesWords()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        testbed.Models.Fails(BacklogTestbed.Sol, 3).Fails(BacklogTestbed.Luna, 3);

        var response = await Function(enabled: true).Decide(
            Amending(await ETag(draft), "Litany of the Cold Cache, Amended", "Warm it up."), draft.Id, "rewrite", CancellationToken.None);

        var html = Html(response.Result, 503);
        Assert.Contains("The Machine Spirit couldn&#39;t rewrite it just now.", html);
        Assert.Contains("""value="Litany of the Cold Cache, Amended" """, html);
    }

    // A summon changes nothing stored itself, so its entry is saved alone, naming who summoned.
    [Fact]
    public async Task Summon_IsRecorded()
    {
        await Function(enabled: true).Summon(Post(""), CancellationToken.None);

        var entry = Assert.Single(Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)));
        Assert.Equal((AuditEntry.StagingScribe, AuditAction.Summon), (entry.Actor, entry.Action));
    }

    [Fact]
    public async Task TheRecord_ListsEachChange_AndWhoMadeIt_AllOrOneRites()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.2f);
        await testbed.AddDraft("The Rite of Re-Run", 0.5f);
        await Function(enabled: true).Decide(Post(""), draft.Id, "burn", CancellationToken.None);

        var all = Html(await Function(enabled: true).Record(Get(""), CancellationToken.None), 200);
        var one = Html(await Function(enabled: true).Record(Get($"?draft={draft.Id:D}"), CancellationToken.None), 200);

        var rite = $"""<a href="/scriptorium/record?draft={draft.Id:D}">“Litany of the Cold Cache”</a>""";
        Assert.Contains($"""A Scribe on staging consigned {rite} to the flames <span class="when">(from #2)</span>""", all);
        Assert.Contains($"The Machine Spirit wrote {rite}", all);
        Assert.Contains("“The Rite of Re-Run”", all);
        Assert.Contains("One rite's record.", one);
        Assert.DoesNotContain("“The Rite of Re-Run”", one);
        var calendar = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);
        Assert.Contains("""<a href="/scriptorium/record">The Record</a>""", calendar);
    }

    [Fact]
    public async Task TheRecord_IsTheScribesAlone()
    {
        var notAScribe = new ScriptoriumFunction(testbed.Scribes(), testbed.Amendments(), testbed.Audit, new NobodyIsScribeSignIn(),
            Options.Create(new ScriptoriumOptions { Enabled = true }), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await notAScribe.Record(Get(""), CancellationToken.None)).StatusCode);
        Assert.IsType<NotFoundResult>(await Function(enabled: false).Record(Get(""), CancellationToken.None));
    }

    private ScriptoriumFunction Function(bool enabled)
        => new(testbed.Scribes(), testbed.Amendments(), testbed.Audit, new EveryoneIsScribeSignIn(), Options.Create(new ScriptoriumOptions { Enabled = enabled }), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

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
            ("note", note),
        }.Select(field => $"{field.Item1}={Uri.EscapeDataString(field.Item2)}")));

    private async Task<string> ETag(Draft draft)
        => Ok(await testbed.Amendments().Get(draft.Id, CancellationToken.None))
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
