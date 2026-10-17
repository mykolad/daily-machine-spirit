using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Precepts;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static DailyMachineSpirit.Tests.PreceptsTests;
using static DailyMachineSpirit.Tests.ScriptoriumRequests;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The Precepts over HTTP (#12): their page, the Scribes' changes, distilling on demand, and the weekly timer.</summary>
public sealed class PreceptsFunctionTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task Page_ShowsEachPrecept_WhereItCameFrom_AndWhatCanBeDone()
    {
        await AJudgment();
        Ok(await testbed.PreceptKeeping().AddPinned("Name the command: `npm ci`.", Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled("Prefer the real fix."));
        Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));

        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("Distilled Wed 7 Oct, 00:00 UTC from 1 judgments, and again every Monday.", html);
        Assert.Contains("""<p class="text">Name the command: <code>npm ci</code>.</p>""", html);
        Assert.Contains("""<span class="placed">Pinned</span> · A Scribe&#39;s words""", html);
        Assert.Contains("Distilled by the Machine Spirit", html);
        Assert.Contains("""Unpin<span class="visually-hidden">: Name the command: `npm ci`.</span>""", html);
        Assert.Contains("""Pin<span class="visually-hidden">: Prefer the real fix.</span>""", html);
        Assert.Contains("<button title=\"Keeps this precept, word for word, when the Precepts are distilled again. Pinned precepts come first.\"", html);
        Assert.Contains("<button title=\"Lets the next distillation replace this precept with a new one.\"", html);
        Assert.Contains("""<label for="precept-new">A precept of your own</label>""", html);
        Assert.Contains("""<a href="/scriptorium">Back to the Scriptorium</a>""", html);
    }

    // A table, so each figure is read with its month and what it counts.
    [Fact]
    public async Task Page_ShowsWhetherTheScribesAgreeWithTheAugury()
    {
        var best = await testbed.AddDraft("Best", 0.9f);
        var worst = await testbed.AddDraft("Worst", 0.3f);
        Ok(await testbed.Scribes().Anoint(worst.Id, "", Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Burn(best.Id, "", Tester, CancellationToken.None));

        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<h2 id="agreement-heading">Do the Scribes Agree with the Augury?</h2>""", html);
        Assert.Contains("""<th scope="col">Top picks burned</th>""", html);
        Assert.Contains("""<tr><th scope="row">Oct 2026</th><td>2</td><td>1 of 1 burn</td><td>1 place (1 move)</td></tr>""", html);
        Assert.Contains("""<tr><th scope="row">Sep 2026</th><td>0</td><td>No burns</td><td>No moves</td></tr>""", html);
    }

    [Fact]
    public async Task Page_BeforeAnyPrecept_SaysSo()
    {
        var html = Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("Not distilled yet", html);
        Assert.Contains("There are no precepts yet.", html);
    }

    // Full, the page offers no way to add one.
    [Fact]
    public async Task Page_WhenFull_HasNoFieldToAddOne()
    {
        for (var added = 0; added < PreceptBook.MaxPrecepts; added++)
        {
            Ok(await testbed.PreceptKeeping().AddPinned($"Precept {added}.", Tester, CancellationToken.None));
        }

        Assert.DoesNotContain("precept-new", Html(await Function(enabled: true).Page(Get(""), CancellationToken.None), 200));
    }

    [Fact]
    public async Task ThePrecepts_AreTheScribesAlone()
    {
        var notAScribe = Function(new NobodyIsScribeSignIn(), enabled: true);
        var forged = Form(("text", "Burn everything."));
        forged.Headers["Sec-Fetch-Site"] = "cross-site";

        Assert.IsType<NotFoundResult>(await Function(enabled: false).Page(Get(""), CancellationToken.None));
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await notAScribe.Page(Get(""), CancellationToken.None)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await notAScribe.Distil(Form(), CancellationToken.None)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await Function(enabled: true).Add(forged, CancellationToken.None)).StatusCode);
        Assert.IsType<NotFoundResult>(await Function(enabled: false).Change(Form(), Guid.NewGuid(), "pin", CancellationToken.None));
        Assert.Empty(Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts);
    }

    [Fact]
    public async Task Distil_GoesBackToThePrecepts_SayingSo()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled("Prefer the real fix."));
        var function = Function(enabled: true);

        var redirect = await function.Distil(Form(), CancellationToken.None);

        Assert.Equal("/scriptorium/precepts?done=distil", RedirectedTo(redirect));
        var html = Html(await function.Page(Following(redirect), CancellationToken.None), 200);
        Assert.Contains("The Precepts were distilled again from the Scribes&#39; judgments.", html);
        Assert.Contains("Prefer the real fix.", html);
    }

    [Fact]
    public async Task Distil_SaysWhyNothingWasDistilled()
    {
        var function = Function(enabled: true);
        Assert.Equal("/scriptorium/precepts?done=distil-nothing", RedirectedTo(await function.Distil(Form(), CancellationToken.None)));

        await AJudgment();
        testbed.Models.Fails(Sol, 1).Fails(Luna, 1);
        Assert.Equal("/scriptorium/precepts?done=distil-failed", RedirectedTo(await function.Distil(Form(), CancellationToken.None)));

        for (var added = 0; added < PreceptBook.MaxPrecepts; added++)
        {
            Ok(await testbed.PreceptKeeping().AddPinned($"Precept {added}.", Tester, CancellationToken.None));
        }
        var redirect = await function.Distil(Form(), CancellationToken.None);
        Assert.Equal("/scriptorium/precepts?done=distil-pinned", RedirectedTo(redirect));
        Assert.Contains("Every precept is pinned", Html(await function.Page(Following(redirect), CancellationToken.None), 200));
    }

    // A distillation saved meanwhile by another Scribe, or the weekly run, is a change to look at.
    [Fact]
    public async Task Distil_WhenThePreceptsChangedMeanwhile_SaysToLookAgain()
    {
        await AJudgment();
        testbed.Models.Then(Sol, [async () =>
        {
            Ok(await testbed.PreceptKeeping().AddPinned("Added while the model thought.", Tester, CancellationToken.None));
            return Distilled("Prefer the real fix.");
        }]);

        Assert.Equal("/scriptorium/precepts?done=changed", RedirectedTo(await Function(enabled: true).Distil(Form(), CancellationToken.None)));
    }

    [Fact]
    public async Task TheScribes_AddEditPinUnpinAndDelete_OverHttp()
    {
        var function = Function(enabled: true);
        Assert.Equal("/scriptorium/precepts?done=add", RedirectedTo(await function.Add(Form(("text", "Prefer the real fix.")), CancellationToken.None)));
        var id = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Id;
        var at = $"&precept={id:D}#precept-{id:D}";

        Assert.Equal($"/scriptorium/precepts?done=edit{at}",
            RedirectedTo(await function.Change(Form(("text", "Name the real fix."), await Seen()), id, "edit", CancellationToken.None)));
        Assert.Equal($"/scriptorium/precepts?done=unpin{at}", RedirectedTo(await function.Change(Form(await Seen()), id, "unpin", CancellationToken.None)));
        var unpinned = await function.Page(Get($"?done=unpin&precept={id:D}"), CancellationToken.None);
        Assert.Contains($"""<li class="rite acted" id="precept-{id:D}">""", Html(unpinned, 200));
        Assert.Equal($"/scriptorium/precepts?done=pin{at}", RedirectedTo(await function.Change(Form(await Seen()), id, "pin", CancellationToken.None)));
        Assert.Equal("Name the real fix.", Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Text);

        Assert.Equal("/scriptorium/precepts?done=delete", RedirectedTo(await function.Change(Form(await Seen()), id, "delete", CancellationToken.None)));
        Assert.Empty(Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts);
        Assert.Equal("/scriptorium/precepts?done=changed", RedirectedTo(await function.Change(Form(await Seen()), id, "pin", CancellationToken.None)));
    }

    // Two Scribes with the same page open: the second, saving after the first, is told to look again rather than
    // overwriting the first one's words unseen.
    [Fact]
    public async Task AChange_FromAPageShownBeforeAnother_IsRefused()
    {
        Ok(await testbed.PreceptKeeping().AddPinned("Prefer the real fix.", Tester, CancellationToken.None));
        var id = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Id;
        var bothSaw = await Seen();
        var function = Function(enabled: true);
        var first = await function.Change(Form(("text", "The first Scribe's words."), bothSaw), id, "edit", CancellationToken.None);
        Assert.StartsWith("/scriptorium/precepts?done=edit", RedirectedTo(first));

        var second = await function.Change(Form(("text", "The second Scribe's words."), bothSaw), id, "edit", CancellationToken.None);

        Assert.Equal("/scriptorium/precepts?done=changed", RedirectedTo(second));
        Assert.Equal("The first Scribe's words.", Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Text);
        Assert.Contains($"""<input type="hidden" name="etag" value="{System.Net.WebUtility.HtmlEncode((await Seen()).Value)}">""",
            Html(await function.Page(Get(""), CancellationToken.None), 200));
    }

    [Fact]
    public async Task TheScribesMistakes_ComeBackAsMessages()
    {
        var function = Function(enabled: true);
        Assert.Equal("/scriptorium/precepts?done=text-needed", RedirectedTo(await function.Add(Form(("text", " ")), CancellationToken.None)));
        Ok(await testbed.PreceptKeeping().AddPinned("Prefer the real fix.", Tester, CancellationToken.None));
        var id = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Id;
        var tooLong = await function.Change(Form(("text", new string('x', Precept.MaxLength + 1))), id, "edit", CancellationToken.None);
        Assert.Equal($"/scriptorium/precepts?done=too-long&precept={id:D}#precept-{id:D}", RedirectedTo(tooLong));
        Assert.Contains("at most 300 characters", Html(await function.Page(Following(tooLong), CancellationToken.None), 200));
        for (var added = 1; added < PreceptBook.MaxPrecepts; added++)
        {
            Ok(await testbed.PreceptKeeping().AddPinned($"Precept {added}.", Tester, CancellationToken.None));
        }
        Assert.Equal("/scriptorium/precepts?done=full", RedirectedTo(await function.Add(Form(("text", "One too many.")), CancellationToken.None)));
    }

    [Fact]
    public async Task OnlyThePagesForms_AndActions_AreTaken()
    {
        var function = Function(enabled: true);
        var notAForm = Form();
        notAForm.ContentType = "application/json";

        Assert.IsType<BadRequestResult>(await function.Add(notAForm, CancellationToken.None));
        Assert.IsType<BadRequestResult>(await function.Change(notAForm, Guid.NewGuid(), "pin", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await function.Change(Form(), Guid.NewGuid(), "exalt", CancellationToken.None));
    }

    [Theory]
    [InlineData("add", "Your precept was added, pinned.")]
    [InlineData("edit", "The precept was changed and pinned: distilling again keeps it.")]
    [InlineData("pin", "Pinned: distilling again keeps it.")]
    [InlineData("unpin", "Unpinned: the next distillation may replace it.")]
    [InlineData("delete", "Deleted. The Machine Spirit won&#39;t propose it again.")]
    [InlineData("distil-nothing", "The Scribes haven&#39;t judged any rite yet, so there&#39;s nothing to learn from.")]
    [InlineData("distil-failed", "The Machine Spirit couldn&#39;t distil them just now. Try again in a moment.")]
    [InlineData("text-needed", "A precept needs words.")]
    [InlineData("full", "There are 12 precepts already: delete or edit one instead.")]
    [InlineData("changed", "The Precepts changed meanwhile: another Scribe, or a distillation. Look again.")]
    public async Task Page_SaysWhatTheLastActionDid(string outcome, string message)
        => Assert.Contains($"""<p class="status" role="status">{message}</p>""",
            Html(await Function(enabled: true).Page(Get($"?done={outcome}"), CancellationToken.None), 200));

    // Cosmos down: the page says the Scriptorium is silent, as its other pages do.
    [Fact]
    public async Task WhenThePreceptsCantBeReadOrSaved_TheScriptoriumIsSilent()
    {
        var unreadable = new FakePrecepts { Stored = Error.New("Cosmos is down.") };
        Assert.Contains("The Scriptorium is silent",
            Html(await Function(unreadable).Page(Get(""), CancellationToken.None), 500));

        var unsaveable = new FakePrecepts { Saving = Error.New("Cosmos is down.") };
        Assert.Contains("The Scriptorium is silent",
            Html(await Function(unsaveable).Add(Form(("text", "Prefer the real fix.")), CancellationToken.None), 500));
    }

    // The record names what was done to the Precepts, and links to them.
    [Fact]
    public async Task TheRecord_ShowsWhatWasDoneToThePrecepts()
    {
        await AJudgment();
        Ok(await testbed.PreceptKeeping().AddPinned("Prefer the real fix.", Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled("Name the command."));
        Ok(await testbed.Distiller().Distil(AuditEntry.MachineSpirit, CancellationToken.None));

        var html = ScriptoriumPage.RenderRecord(Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)), [], None);

        Assert.Contains("added the precept “Prefer the real fix.”", html);
        Assert.Contains("""The Machine Spirit distilled <a href="/scriptorium/precepts">the Precepts</a> <span class="when">(1 new, 1 pinned, from 1 judgments)</span>""", html);
    }

    [Fact]
    public async Task TheWeeklyRun_DistilsAsTheMachineSpirit()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled("Prefer the real fix."));

        await Timer(enabled: true).Run(new TimerInfo(), CancellationToken.None);

        Assert.Equal("Prefer the real fix.", Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Text);
        Assert.Contains(Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)),
            entry => entry is { Action: AuditAction.Distil, Actor: AuditEntry.MachineSpirit });
    }

    // Without the Scriptorium, or anything to learn from, there's nothing to do; a failure fails the run, to be retried.
    [Fact]
    public async Task TheWeeklyRun_IsQuiet_WithNothingToDo_AndFailsWhenTheModelsDo()
    {
        await Timer(enabled: true).Run(new TimerInfo(), CancellationToken.None);
        await AJudgment();
        await Timer(enabled: false).Run(new TimerInfo(), CancellationToken.None);
        Assert.Empty(testbed.Models.Requests);

        testbed.Models.Fails(Sol, 1).Fails(Luna, 1);
        await Assert.ThrowsAnyAsync<Exception>(() => Timer(enabled: true).Run(new TimerInfo(), CancellationToken.None));
    }

    private PreceptsFunction Function(bool enabled) => Function(new EveryoneIsScribeSignIn(), enabled);

    private PreceptsFunction Function(IScribeSignIn signIn, bool enabled)
        => new(testbed.PreceptKeeping(), testbed.Distiller(), new AgreementTrend(testbed.Scriptorium, testbed.Time), signIn,
            Options.Create(new ScriptoriumOptions { Enabled = enabled }), NullLogger<PreceptsFunction>.Instance);

    private PreceptsFunction Function(FakePrecepts precepts)
        => new(new PreceptKeeping(precepts, testbed.Time),
            new PreceptDistiller(testbed.Scriptorium, precepts, testbed.Models, Options.Create(new GenerationOptions()), testbed.Time, testbed.Probe.Metrics, NullLogger<PreceptDistiller>.Instance),
            new AgreementTrend(testbed.Scriptorium, testbed.Time),
            new EveryoneIsScribeSignIn(), Options.Create(new ScriptoriumOptions { Enabled = true }), NullLogger<PreceptsFunction>.Instance);

    private DistilPreceptsFunction Timer(bool enabled)
        => new(testbed.Distiller(), Options.Create(new ScriptoriumOptions { Enabled = enabled }), NullLogger<DistilPreceptsFunction>.Instance);

    // The version of the Precepts the page shows now, as its forms send it.
    private async Task<(string Name, string Value)> Seen()
        => ("etag", Ok(await testbed.Precepts.Get(CancellationToken.None)).ETag.IfNone(string.Empty));

    private async Task AJudgment()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", Tester, CancellationToken.None));
    }
}
