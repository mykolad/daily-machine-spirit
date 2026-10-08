using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
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
        Ok(await testbed.Scribes().Burn(burned.Id, "Spinners <again>.", CancellationToken.None));
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
    public async Task Page_SaysWhatTheLastActionDid()
    {
        var html = Html(await Function(enabled: true).Page(Get("burn"), CancellationToken.None), 200);

        Assert.Contains("Consigned to the flames.", html);
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
            testbed.ScribesWithoutCosmos(), Options.Create(new ScriptoriumOptions { Enabled = true }), NullLogger<ScriptoriumFunction>.Instance);

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

        Assert.Equal($"/scriptorium?done={action}", Assert.IsType<RedirectResult>(response.Result).Url);
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

        Assert.Equal("/scriptorium?done=burn", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Null(response.RefillReason);
    }

    [Fact]
    public async Task BurningTheLastDraft_AsksForARefillAtOnce()
    {
        var last = await testbed.AddDraft("The last one", 0.5f);

        var response = await Function(enabled: true).Decide(Post(""), last.Id, "burn", CancellationToken.None);

        Assert.Equal("/scriptorium?done=burn-last", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Equal(ScriptoriumFunction.AllBurned, response.RefillReason);
        Assert.Empty(await testbed.Waiting());
    }

    [Fact]
    public async Task Restoring_GoesBackToThePage()
    {
        var draft = await testbed.AddDraft("Second chance", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", CancellationToken.None));

        var response = await Function(enabled: true).Decide(Post(""), draft.Id, "restore", CancellationToken.None);

        Assert.Equal("/scriptorium?done=restore", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public async Task ActingOnADraftThatChanged_SaysSo_InsteadOfFailing()
    {
        var response = await Function(enabled: true).Decide(Post(""), Guid.NewGuid(), "burn", CancellationToken.None);

        Assert.Equal("/scriptorium?done=changed", Assert.IsType<RedirectResult>(response.Result).Url);
    }

    [Fact]
    public async Task ANoteTooLong_SaysSo_AndChangesNothing()
    {
        var draft = await testbed.AddDraft("Kept", 0.5f);

        var response = await Function(enabled: true).Decide(
            Post(new string('a', ScribeDecision.MaxNoteLength + 1)), draft.Id, "humble", CancellationToken.None);

        Assert.Equal("/scriptorium?done=note-too-long", Assert.IsType<RedirectResult>(response.Result).Url);
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
        Assert.IsType<NotFoundResult>(function.Summon(Post("")).Result);
        Assert.IsType<NotFoundResult>(await function.LetTheAuguryDecide(Post(""), CancellationToken.None));
        Assert.Single(await testbed.Waiting());
    }

    [Fact]
    public void Summon_AsksForARefill_AndGoesBackToThePage()
    {
        var response = Function(enabled: true).Summon(Post(""));

        Assert.Equal("/scriptorium?done=summon", Assert.IsType<RedirectResult>(response.Result).Url);
        Assert.Equal(ScriptoriumFunction.Summoned, response.RefillReason);
    }

    [Fact]
    public async Task LetTheAuguryDecide_ForgetsTheScribesOrder_AndGoesBackToThePage()
    {
        await testbed.AddDraft("Excellent", 0.95f);
        var fair = await testbed.AddDraft("Fair", 0.4f);
        Ok(await testbed.Scribes().Anoint(fair.Id, "", CancellationToken.None));

        var result = await Function(enabled: true).LetTheAuguryDecide(Post(""), CancellationToken.None);

        Assert.Equal("/scriptorium?done=augury", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal("Excellent", Ok(await testbed.Scribes().View(CancellationToken.None)).Calendar[0].Draft.Title);
    }

    private ScriptoriumFunction Function(bool enabled)
        => new(testbed.Scribes(), Options.Create(new ScriptoriumOptions { Enabled = enabled }), NullLogger<ScriptoriumFunction>.Instance);

    private static HttpRequest Get(string done)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "GET";
        if (done.Length > 0)
            request.QueryString = new QueryString($"?done={done}");
        return request;
    }

    // As a browser posts the page's form: same origin, with the note field.
    private static HttpRequest Post(string note)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "POST";
        request.Host = new HostString("dailymachinespirit.fyi");
        request.Headers["Sec-Fetch-Site"] = "same-origin";
        request.ContentType = "application/x-www-form-urlencoded";
        request.Form = new FormCollection(new Dictionary<string, StringValues> { ["note"] = note });
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
