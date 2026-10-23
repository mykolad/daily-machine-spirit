using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
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
    // How the page is shown when a test renders it directly: nothing open to edit, nobody named.
    private static readonly PreceptsShown Shown = new(None, new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc), None, []);

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

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<p class="distilled-at">Distilled Wed 7 Oct, 00:00 UTC</p>""", html);
        Assert.Contains("From 1 judgments, and again every Monday (next: Mon 12 Oct).", html);
        Assert.Contains("""<span class="numeral" aria-hidden="true">I</span>""", html);
        Assert.Contains("""<span class="numeral" aria-hidden="true">II</span>""", html);
        Assert.Contains("""<p class="precept-text" title="Click to edit">Name the command: <code>npm ci</code>.</p>""", html);
        Assert.Contains($"""<span class="pinned-by">{ScriptoriumIcons.PinFilled}Written by A Scribe (…456789) · pinned</span>""", html);
        Assert.Contains("""<p class="precept-meta">Distilled by the Machine Spirit</p>""", html);
        Assert.Contains("""aria-pressed="true" title="Unpin: the next distillation may replace it with a new one.">""", html);
        Assert.Contains("""aria-pressed="false" title="Pin: distilling again keeps it, word for word. Pinned precepts come first.">""", html);
        Assert.Contains("""<span class="visually-hidden">Pin, so distilling keeps it: Prefer the real fix.</span>""", html);
        Assert.Contains("""<span class="visually-hidden">Edit: Name the command: `npm ci`.</span>""", html);
        Assert.Contains("""<span class="visually-hidden">Delete: Prefer the real fix.</span>""", html);
        Assert.Contains("""<span class="numeral" aria-hidden="true">III</span>""", html);
        Assert.Contains("""<label for="precept-new">A precept of your own <span class="optional">(kept pinned)</span></label>""", html);
        Assert.Contains("""<a href="/scriptorium">The Calendar""", html);
        Assert.Contains("Signed in as <strong>", html);
    }

    // Whose words each precept is, and who pinned it: the Scribe who wrote or amended it, or who pinned a distilled one.
    [Fact]
    public async Task Page_SaysWhoWroteAmendedOrPinnedEachPrecept()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled("Prefer the real fix.", "Name the command.", "Keep it short."));
        Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));
        var distilled = Ok(await testbed.Precepts.Get(CancellationToken.None));
        var (first, second) = (distilled.Book.Precepts[0], distilled.Book.Precepts[1]);
        Ok(await testbed.PreceptKeeping().Pin(first.Id, distilled.ETag.IfNone(""), Tester, CancellationToken.None));
        Ok(await testbed.PreceptKeeping().EditAndPin(second.Id, Ok(await testbed.Precepts.Get(CancellationToken.None)).ETag.IfNone(""),
            "Name the one command.", Tester, CancellationToken.None));

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("Pinned by A Scribe (…456789)</span>", html);
        Assert.Contains("Amended by A Scribe (…456789) · pinned</span>", html);
        Assert.Contains("""<p class="precept-meta">Distilled by the Machine Spirit</p>""", html);
    }

    // Without the script, the pencil is a link to the page with the precept's words open to edit.
    [Fact]
    public async Task Page_OpensAPreceptsWordsToEdit_WhenAsked()
    {
        var id = Ok(await testbed.PreceptKeeping().AddPinned("Name the command.", Tester, CancellationToken.None));

        var html = Html(await Function().Page(Get($"?edit={id:D}"), CancellationToken.None), 200);
        var unasked = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains($"""<li class="precept pinned editing" id="precept-{id:D}">""", html);
        Assert.Contains($"""<a class="icon-button edit" href="/scriptorium/precepts?edit={id:D}#precept-{id:D}" """, unasked);
        Assert.Contains($"""<li class="precept pinned" id="precept-{id:D}">""", unasked);
    }

    [Theory]
    [InlineData(2026, 10, 7, 12, "2026-10-12T01:00:00")]
    [InlineData(2026, 10, 12, 0, "2026-10-12T01:00:00")]
    [InlineData(2026, 10, 12, 1, "2026-10-19T01:00:00")]
    [InlineData(2026, 10, 18, 23, "2026-10-19T01:00:00")]
    public void NextDistillation_IsTheComingMondayAtOne(int year, int month, int day, int hour, string expected)
        => Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            ScriptoriumPage.NextDistillation(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc)));

    // Just distilled, the new precepts are marked, so the eye finds them; the Scribe's pinned one stayed, so it isn't.
    // Distilling takes seconds, which its button says when pressed.
    [Fact]
    public async Task Page_AfterDistilling_MarksTheNewPrecepts()
    {
        await AJudgment();
        Ok(await testbed.PreceptKeeping().AddPinned("Name the command.", Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, Distilled("Prefer the real fix."));
        Ok(await testbed.Distiller().Distil(Tester, CancellationToken.None));
        var book = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book;

        var html = Html(await Function().Page(Get("?done=distil"), CancellationToken.None), 200);

        Assert.Contains($"""<li class="precept acted" id="precept-{book.Precepts.Single(precept => !precept.Pinned).Id:D}">""", html);
        Assert.Contains($"""<li class="precept pinned" id="precept-{book.Precepts.Single(precept => precept.Pinned).Id:D}">""", html);
        Assert.Contains("""data-slow="The Machine Spirit is distilling the Precepts…" data-busy-label="Distilling…">""", html);
        Assert.Contains(SlowActionScript.Js, html);
    }

    // A table, so each figure is read with its month and what it counts.
    [Fact]
    public async Task Page_ShowsWhetherTheScribesAgreeWithTheAugury()
    {
        var best = await testbed.AddDraft("Best", 0.9f);
        var worst = await testbed.AddDraft("Worst", 0.3f);
        Ok(await testbed.Scribes().Anoint(worst.Id, "", Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Burn(best.Id, "", Tester, CancellationToken.None));

        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""<h2 id="agreement-heading">Do the Scribes Agree with the Augury?</h2>""", html);
        Assert.Contains("""<th scope="col" class="number">Top picks burned</th>""", html);
        Assert.Contains("""<tr><th scope="row">Oct 2026</th><td class="number">2</td><td class="number">1 <span class="muted">of 1 burn</span></td><td class="number">1 place <span class="muted">(1 move)</span></td></tr>""", html);
        // The five months before, all without a judgment, share one row.
        Assert.Contains("""<tr class="quiet"><th scope="row">May–Sep 2026</th><td colspan="3">No judgments</td></tr>""", html);
    }

    [Fact]
    public async Task Page_BeforeAnyPrecept_SaysSo()
    {
        var html = Html(await Function().Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("Not distilled yet", html);
        Assert.Contains("There are no precepts yet: distil them, or write one of your own.", html);
        Assert.Contains("They're distilled every Monday from the Scribes' judgments (next: Mon 12 Oct).", html.Replace("&#39;", "'"));
    }

    // Full, the page offers no way to add one.
    [Fact]
    public async Task Page_WhenFull_HasNoFieldToAddOne()
    {
        for (var added = 0; added < PreceptBook.MaxPrecepts; added++)
        {
            Ok(await testbed.PreceptKeeping().AddPinned($"Precept {added}.", Tester, CancellationToken.None));
        }

        Assert.DoesNotContain("precept-new", Html(await Function().Page(Get(""), CancellationToken.None), 200));
    }

    [Fact]
    public async Task ThePrecepts_AreTheScribesAlone()
    {
        var notAScribe = Function(new NobodyIsScribeSignIn());
        var forged = Form(("text", "Burn everything."));
        forged.Headers["Sec-Fetch-Site"] = "cross-site";

        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await notAScribe.Page(Get(""), CancellationToken.None)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await notAScribe.Distil(Form(), CancellationToken.None)).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(await Function().Add(forged, CancellationToken.None)).StatusCode);
        Assert.Empty(Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts);
    }

    [Fact]
    public async Task Distil_GoesBackToThePrecepts_SayingSo()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled("Prefer the real fix."));
        var function = Function();

        var redirect = await function.Distil(Form(), CancellationToken.None);

        Assert.Equal("/scriptorium/precepts?done=distil", RedirectedTo(redirect));
        var html = Html(await function.Page(Following(redirect), CancellationToken.None), 200);
        Assert.Contains("The Precepts were distilled again from the Scribes&#39; judgments.", html);
        Assert.Contains("Prefer the real fix.", html);
    }

    [Fact]
    public async Task Distil_SaysWhyNothingWasDistilled()
    {
        var function = Function();
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

        Assert.Equal("/scriptorium/precepts?done=changed", RedirectedTo(await Function().Distil(Form(), CancellationToken.None)));
    }

    [Fact]
    public async Task TheScribes_AddEditPinUnpinAndDelete_OverHttp()
    {
        var function = Function();
        var added = RedirectedTo(await function.Add(Form(("text", "Prefer the real fix.")), CancellationToken.None));
        var id = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Id;
        var at = $"&precept={id:D}#precept-{id:D}";
        Assert.Equal($"/scriptorium/precepts?done=add{at}", added);

        Assert.Equal($"/scriptorium/precepts?done=edit{at}",
            RedirectedTo(await function.Change(Form(("text", "Name the real fix."), await Seen()), id, "edit", CancellationToken.None)));
        Assert.Equal($"/scriptorium/precepts?done=unpin{at}", RedirectedTo(await function.Change(Form(await Seen()), id, "unpin", CancellationToken.None)));
        var unpinned = await function.Page(Get($"?done=unpin&precept={id:D}"), CancellationToken.None);
        Assert.Contains($"""<li class="precept acted" id="precept-{id:D}">""", Html(unpinned, 200));
        Assert.Equal($"/scriptorium/precepts?done=pin{at}", RedirectedTo(await function.Change(Form(await Seen()), id, "pin", CancellationToken.None)));
        Assert.Equal("Name the real fix.", Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Text);

        Assert.Equal($"/scriptorium/precepts?done=delete&precept={id:D}", RedirectedTo(await function.Change(Form(await Seen()), id, "delete", CancellationToken.None)));
        Assert.Empty(Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts);
        Assert.Equal("/scriptorium/precepts?done=changed", RedirectedTo(await function.Change(Form(await Seen()), id, "pin", CancellationToken.None)));
    }

    // A delete takes the precept away at once, and offers to undo it: Undo puts it back where it was, as it was, and a
    // distillation may propose its words again. Only straight after: any change since makes the Undo look again.
    [Fact]
    public async Task Undo_AfterADelete_PutsThePreceptBackWhereItWas()
    {
        var function = Function();
        var first = Ok(await testbed.PreceptKeeping().AddPinned("First.", Tester, CancellationToken.None));
        var middle = Ok(await testbed.PreceptKeeping().AddPinned("Middle.", Tester, CancellationToken.None));
        Ok(await testbed.PreceptKeeping().AddPinned("Last.", Tester, CancellationToken.None));

        await function.Change(Form(await Seen()), middle, "delete", CancellationToken.None);
        var html = Html(await function.Page(Get($"?done=delete&precept={middle:D}"), CancellationToken.None), 200);

        Assert.Contains($"""<div class="toast" data-fades><span class="toast-text">Precept deleted.</span><form method="post" action="/scriptorium/precepts/{middle:D}/restore">""", html);
        Assert.Contains("""<p class="status" role="status" id="precepts-status"></p>""", html);
        Assert.Contains("Middle.", Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Rejected);

        Assert.Equal($"/scriptorium/precepts?done=restore&precept={middle:D}#precept-{middle:D}",
            RedirectedTo(await function.Change(Form(await Seen()), middle, "restore", CancellationToken.None)));
        var book = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book;
        Assert.Equal(["First.", "Middle.", "Last."], book.Precepts.Select(precept => precept.Text));
        Assert.Equal(PreceptOrigin.Written, book.Precepts[1].Origin);
        Assert.DoesNotContain("Middle.", book.Rejected);
        Assert.Equal("/scriptorium/precepts?done=changed", RedirectedTo(await function.Change(Form(await Seen()), middle, "restore", CancellationToken.None)));

        await function.Change(Form(await Seen()), middle, "delete", CancellationToken.None);
        await function.Change(Form(await Seen()), first, "unpin", CancellationToken.None);
        Assert.Equal("/scriptorium/precepts?done=changed", RedirectedTo(await function.Change(Form(await Seen()), middle, "restore", CancellationToken.None)));
        Assert.Equal(["Last.", "First."], Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Select(precept => precept.Text));
    }

    // Two Scribes with the same page open: the second, saving after the first, is told to look again rather than
    // overwriting the first one's words unseen.
    [Fact]
    public async Task AChange_FromAPageShownBeforeAnother_IsRefused()
    {
        Ok(await testbed.PreceptKeeping().AddPinned("Prefer the real fix.", Tester, CancellationToken.None));
        var id = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Id;
        var bothSaw = await Seen();
        var function = Function();
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
        var function = Function();
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

    // A precept breaking the writer's rules comes back on the page, not saved, with the Scribe's words in their field and
    // why: a redirect couldn't carry them.
    [Fact]
    public async Task APrecept_BreakingARule_ComesBack_WithTheScribesWordsAndWhy()
    {
        var function = Function();
        testbed.Models.Answers(Checker, PreceptsTests.Breaking((1, "It mocks a group of people.")));

        var html = Html(await function.Add(Form(("text", "Mock <managers>.")), CancellationToken.None), 422);

        Assert.Contains("""<p class="status" role="status" id="precepts-status">Not saved: It mocks a group of people. Reword it, or leave it as it was.</p>""", html);
        Assert.Contains("""aria-invalid="true" aria-describedby="precepts-status" required>Mock &lt;managers&gt;.</textarea>""", html);
        Assert.Empty(Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts);
    }

    // Unchecked, it comes back the same way, with what to do: try again, the words still in their field.
    [Fact]
    public async Task APrecept_NoModelCanCheck_ComesBack_ToTryAgain()
    {
        var function = Function();
        testbed.Models.Fails(Checker, 1);
        testbed.Models.Fails(SecondChecker, 1);

        var html = Html(await function.Add(Form(("text", "Prefer the real fix.")), CancellationToken.None), 422);

        Assert.Contains("""<p class="status" role="status" id="precepts-status">Not saved: The rules couldn&#39;t be checked just now. Try again in a moment.</p>""", html);
        Assert.Contains("""aria-invalid="true" aria-describedby="precepts-status" required>Prefer the real fix.</textarea>""", html);
        Assert.Empty(Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts);
    }

    // Asked to distil, the Scribe is told when the distilled precepts couldn't be checked, and that nothing changed.
    [Fact]
    public async Task Distil_WhenTheCheckCantAnswer_SaysThePreceptsStayed()
    {
        Ok(await testbed.Scribes().Burn((await testbed.AddDraft("The Rite of Re-Run", 0.9f)).Id, "Too long.", Tester, CancellationToken.None));
        testbed.Models.Answers(Sol, PreceptsTests.Distilled("Prefer the real fix."));
        testbed.Models.Fails(Checker, 1);
        testbed.Models.Fails(SecondChecker, 1);

        Assert.Equal("/scriptorium/precepts?done=distil-unchecked",
            RedirectedTo(await Function().Distil(Form(), CancellationToken.None)));
        Assert.Contains("couldn&#39;t be checked against the writer&#39;s rules just now, so the Precepts stay as they were. Try again in a moment.",
            ScriptoriumPage.RenderPrecepts(new StoredPrecepts(new PreceptBook(), None), None, [], Some("distil-unchecked"), None, None, Shown));
    }

    // While the check ran, the precept being edited was deleted or replaced, or the Precepts filled up: the field is
    // gone, so the words come back in the message rather than being lost, or an edit becoming an addition.
    [Fact]
    public void ARefusedPrecept_WhoseFieldIsGone_ComesBackInTheMessage()
    {
        var refusal = "It mocks a group of people.";
        var full = new PreceptBook
        {
            Precepts = Enumerable.Range(1, PreceptBook.MaxPrecepts).Select(n => new Precept { Id = Guid.NewGuid(), Text = $"Precept {n}.", Pinned = true }).ToList(),
        };

        var edited = ScriptoriumPage.RenderPrecepts(new StoredPrecepts(new PreceptBook(), None), None, [], None, None,
            Some(new RefusedPrecept(Some(Guid.NewGuid()), "Mock the managers.", refusal, "Reword it, or leave it as it was.")), Shown);
        var added = ScriptoriumPage.RenderPrecepts(new StoredPrecepts(full, None), None, [], None, None,
            Some(new RefusedPrecept(None, "Mock the managers.", refusal, "Reword it, or leave it as it was.")), Shown);

        Assert.Contains(
            "Not saved: It mocks a group of people. The precept changed meanwhile (another Scribe, or a distillation), so it isn&#39;t on the page any more. Your words were: “Mock the managers.”",
            edited);
        Assert.Contains("The Precepts filled up meanwhile, so there&#39;s no room for it now. Your words were: “Mock the managers.”", added);
    }

    [Fact]
    public async Task AnEdit_BreakingARule_ComesBack_OpenOnTheScribesWords()
    {
        testbed.Models.Answers(Checker, PreceptsTests.Breaking());
        Ok(await testbed.PreceptKeeping().AddPinned("Prefer the real fix.", Tester, CancellationToken.None));
        var id = Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Id;
        testbed.Models.Answers(Checker, PreceptsTests.Breaking((1, "It asks for the setting's names.")));

        var html = Html(await Function().Change(Form(("text", "Use Imperium words."), await Seen()), id, "edit", CancellationToken.None), 422);

        Assert.Contains($"""<li class="precept pinned acted editing" id="precept-{id:D}">""", html);
        Assert.Contains($"""aria-describedby="precept-{id:D}-hint precepts-status" aria-invalid="true" required>Use Imperium words.</textarea>""", html);
        Assert.Equal("Prefer the real fix.", Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Text);
    }

    [Fact]
    public async Task OnlyThePagesForms_AndActions_AreTaken()
    {
        var function = Function();
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
        => Assert.Contains($"""<p class="status" role="status" id="precepts-status">{message}</p>""",
            Html(await Function().Page(Get($"?done={outcome}"), CancellationToken.None), 200));

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

        var html = ScriptoriumPage.RenderRecord(Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)), [], None, None);

        Assert.Contains("added the precept “Prefer the real fix.”", html);
        Assert.Contains("""The Machine Spirit distilled <a href="/scriptorium/precepts">the Precepts</a> <span class="when">(1 new, 1 pinned, from 1 judgments)</span>""", html);
    }

    [Fact]
    public async Task TheWeeklyRun_DistilsAsTheMachineSpirit()
    {
        await AJudgment();
        testbed.Models.Answers(Sol, Distilled("Prefer the real fix."));

        await Timer().Run(new TimerInfo(), CancellationToken.None);

        Assert.Equal("Prefer the real fix.", Ok(await testbed.Precepts.Get(CancellationToken.None)).Book.Precepts.Single().Text);
        Assert.Contains(Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)),
            entry => entry is { Action: AuditAction.Distil, Actor: AuditEntry.MachineSpirit });
    }

    // With nothing to learn from, there's nothing to do; a failure fails the run, to be retried.
    [Fact]
    public async Task TheWeeklyRun_IsQuiet_WithNothingToDo_AndFailsWhenTheModelsDo()
    {
        await Timer().Run(new TimerInfo(), CancellationToken.None);
        Assert.Empty(testbed.Models.Requests);

        await AJudgment();
        testbed.Models.Fails(Sol, 1).Fails(Luna, 1);
        await Assert.ThrowsAnyAsync<Exception>(() => Timer().Run(new TimerInfo(), CancellationToken.None));
    }

    private PreceptsFunction Function() => Function(new EveryoneIsScribeSignIn());

    private PreceptsFunction Function(IScribeSignIn signIn)
        => new(testbed.PreceptKeeping(), testbed.Distiller(), new AgreementTrend(testbed.Scriptorium, Options.Create(Settings.ScriptoriumPages), testbed.Time), testbed.Tabs(), testbed.NamesShown(), signIn, testbed.Time, NullLogger<PreceptsFunction>.Instance);

    private PreceptsFunction Function(FakePrecepts precepts)
        => new(new PreceptKeeping(precepts, testbed.PreceptCheck(), testbed.Time, NullLogger<PreceptKeeping>.Instance),
            new PreceptDistiller(testbed.Scriptorium, precepts, testbed.Models, testbed.PreceptCheck(), Options.Create(Settings.PreceptDistilling), testbed.Time, testbed.Probe.Metrics, NullLogger<PreceptDistiller>.Instance),
            new AgreementTrend(testbed.Scriptorium, Options.Create(Settings.ScriptoriumPages), testbed.Time),
            new TabCounting(testbed.Drafts, testbed.Scriptorium, precepts, testbed.Audit, NullLogger<TabCounting>.Instance),
            testbed.NamesShown(), new EveryoneIsScribeSignIn(), testbed.Time, NullLogger<PreceptsFunction>.Instance);

    private DistilPreceptsFunction Timer()
        => new(testbed.Distiller(), NullLogger<DistilPreceptsFunction>.Instance);

    // The version of the Precepts the page shows now, as its forms send it.
    private async Task<(string Name, string Value)> Seen()
        => ("etag", Ok(await testbed.Precepts.Get(CancellationToken.None)).ETag.IfNone(string.Empty));

    private async Task AJudgment()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.5f);
        Ok(await testbed.Scribes().Burn(draft.Id, "", Tester, CancellationToken.None));
    }
}
