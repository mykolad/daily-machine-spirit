using System.Globalization;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using DailyMachineSpirit.Functions.Tags;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Functions.Scriptorium.ScriptoriumHttp;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>A Scriptorium answer: the page to go to, and a refill request when one is needed.</summary>
public sealed class ScriptoriumResponse
{
    [HttpResult]
    public IActionResult Result { get; init; } = new NotFoundResult();

    // Null when nothing needs refilling: the binding sends no message then.
    [QueueOutput(RefillBacklogFunction.QueueName)]
    public string? RefillReason { get; init; }
}

/// <summary>
/// <c>/scriptorium</c>: the Scribes' page, and the actions it posts. Each action redirects back to the page with what
/// happened (post, redirect, get), so reloading never repeats it.
/// </summary>
public sealed class ScriptoriumFunction
{
    public const string Summoned = "summoned by a Scribe";
    public const string AllBurned = "every draft was burned";
    public const string Uncounted = "the backlog couldn't be counted after a burn";

    private readonly Scribes scribes;
    private readonly Amendments amendments;
    private readonly JudgingAgain judgingAgain;
    private readonly IAuditRepository audit;
    private readonly ScribeNaming naming;
    private readonly TabCounting tabs;
    private readonly IOptions<ScriptoriumPagesOptions> pages;
    private readonly TimeProvider time;
    private readonly ScriptoriumGate gate;
    private readonly ILogger<ScriptoriumFunction> logger;

    public ScriptoriumFunction(
        Scribes scribes,
        Amendments amendments,
        JudgingAgain judgingAgain,
        IAuditRepository audit,
        ScribeNaming naming,
        TabCounting tabs,
        IOptions<ScriptoriumPagesOptions> pages,
        IScribeSignIn signIn,
        TimeProvider time,
        ILogger<ScriptoriumFunction> logger)
    {
        this.scribes = scribes;
        this.amendments = amendments;
        this.judgingAgain = judgingAgain;
        this.audit = audit;
        this.naming = naming;
        this.tabs = tabs;
        this.pages = pages;
        this.time = time;
        gate = new ScriptoriumGate(signIn);
        this.logger = logger;
    }

    [Function("Scriptorium")]
    public async Task<IActionResult> Page(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium")] HttpRequest request, CancellationToken cancellationToken)
    {
        var looking = await gate.LookingScribe(request, cancellationToken);
        if (looking.Case is IActionResult refused)
        {
            return refused;
        }
        var scribe = (string)looking.Case;

        var outcome = Optional(request.Query["done"].ToString()).Filter(done => done.Length > 0);
        var rite = Optional(request.Query["rite"].ToString()).Bind(text => Guid.TryParse(text, out var id) ? Some(id) : None);
        var shown = new CalendarShown(Subject(request.Query["subject"].ToString()), request.Query["open"].ToString() == "all");
        var name = await NameOf(scribe, cancellationToken);
        var names = await Names(cancellationToken);
        var counts = await tabs.Count(cancellationToken);
        return await (await scribes.View(cancellationToken)).Match(
            Right: async view => Html(request,
                ScriptoriumPage.Render(view, shown, counts, outcome, rite,
                    await BurnedUnlessShown(rite, id => view.Calendar.Exists(entry => entry.Draft.Id == id), cancellationToken), name, names),
                StatusCodes.Status200OK),
            Left: error => Task.FromResult(Failed(request, error)));
    }

    /// <summary>The Ashes: the burned rites, to restore; <c>?rite=</c> marks the one a burn just sent there.</summary>
    [Function("ScriptoriumAshes")]
    public async Task<IActionResult> Ashes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/ashes")] HttpRequest request, CancellationToken cancellationToken)
    {
        if ((await gate.Refusal(request, cancellationToken)).Case is IActionResult refused)
        {
            return refused;
        }

        var rite = Optional(request.Query["rite"].ToString()).Bind(text => Guid.TryParse(text, out var id) ? Some(id) : None);
        var counts = await tabs.Count(cancellationToken);
        return await (await scribes.Ashes(cancellationToken)).Match(
            Right: async ashes => Html(request,
                // A burn's message links to its rite, which newer burns may have pushed past the ones shown.
                ScriptoriumPage.RenderAshes(ashes, await BurnedUnlessShown(rite, id => ashes.Exists(ash => ash.Id == id), cancellationToken), counts, rite),
                StatusCodes.Status200OK),
            Left: error => Task.FromResult(Failed(request, error)));
    }

    /// <summary>The page for amending a waiting draft's words (#8); its form posts the amend and rewrite actions.</summary>
    [Function("ScriptoriumAmend")]
    public async Task<IActionResult> AmendPage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/drafts/{draftId:guid}/amend")] HttpRequest request,
        Guid draftId,
        CancellationToken cancellationToken)
    {
        if ((await gate.Refusal(request, cancellationToken)).Case is IActionResult refused)
        {
            return refused;
        }

        return (await amendments.FindWaiting(draftId, cancellationToken)).Match(
            Right: found => found.Match(
                Some: amendable => Html(request, ScriptoriumPage.RenderAmend(amendable, Words(amendable.Draft), string.Empty, None, None), StatusCodes.Status200OK),
                // Burned or published since the calendar was shown: the page says to look again.
                None: () => Back("changed", draftId, None)),
            Left: error => Failed(request, error));
    }

    /// <summary>The Record (#9): every change and who made it, newest first; <c>?draft=</c> for one draft's.</summary>
    [Function("ScriptoriumRecord")]
    public async Task<IActionResult> Record(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/record")] HttpRequest request, CancellationToken cancellationToken)
    {
        if ((await gate.Refusal(request, cancellationToken)).Case is IActionResult refused)
        {
            return refused;
        }

        var draft = Optional(request.Query["draft"].ToString()).Bind(text => Guid.TryParse(text, out var id) ? Some(id) : None);
        var entries = await draft.Match(
            Some: id => audit.GetHistory(id, cancellationToken),
            None: () => audit.GetNewest(pages.Value.RecordEntriesShown, cancellationToken));
        var names = await Names(cancellationToken);
        var counts = await tabs.Count(cancellationToken);
        return entries.Match(
            Right: entries => Html(request, ScriptoriumPage.RenderRecord(entries, names, counts, draft), StatusCodes.Status200OK),
            Left: error => Failed(request, error));
    }

    [Function("ScriptoriumDecision")]
    public async Task<ScriptoriumResponse> Decide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/drafts/{draftId:guid}/{action:alpha}")] HttpRequest request,
        Guid draftId,
        string action,
        CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(new ScriptoriumResponse { Result = refused }),
            Right: scribe => Act(request, draftId, action, scribe, cancellationToken));

    [Function("ScriptoriumSummon")]
    public async Task<ScriptoriumResponse> Summon(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/summon")] HttpRequest request, CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(new ScriptoriumResponse { Result = refused }),
            Right: async scribe =>
            {
                // The summon changes nothing stored itself (the refill writes the drafts, each recorded as the Machine
                // Spirit's), so its entry is saved alone. Failing, it's logged, and the summon still goes ahead.
                (await audit.Add(AuditEntries.Of(time, scribe, AuditAction.Summon, None, None), cancellationToken))
                    .IfLeft(error => logger.LogError(error.ToException(), "A summon wasn't recorded: {Reason}", error.Message));
                logger.CountScribeAction("summon");
                return new ScriptoriumResponse { Result = BackToPage("summon", await SubjectOf(request, cancellationToken)), RefillReason = Summoned };
            });

    [Function("ScriptoriumAugury")]
    public async Task<IActionResult> LetTheAuguryDecide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/augury")] HttpRequest request, CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(refused),
            Right: async scribe =>
            {
                var subject = await SubjectOf(request, cancellationToken);
                return Answer(request, "augury", None, subject, await scribes.LetTheAuguryDecide(scribe, cancellationToken)).Result;
            });

    private async Task<ScriptoriumResponse> Act(HttpRequest request, Guid draftId, string action, string scribe, CancellationToken cancellationToken)
        => await (await ReadForm(request, cancellationToken)).Match(
            Some: fields => Act(request, draftId, action, fields, scribe, cancellationToken),
            None: () => Task.FromResult(new ScriptoriumResponse { Result = new BadRequestResult() }));

    private async Task<ScriptoriumResponse> Act(
        HttpRequest request, Guid draftId, string action, IFormCollection fields, string scribe, CancellationToken cancellationToken)
    {
        var note = fields["note"].ToString();
        var subject = Subject(fields["subject"].ToString());
        return action switch
        {
            "amend" => await Amend(request, draftId, fields, scribe, cancellationToken),
            "rewrite" => await Rewrite(request, draftId, fields, cancellationToken),
            "anoint" => Answer(request, action, draftId, subject, await scribes.Anoint(draftId, note, scribe, cancellationToken)),
            "exalt" => Answer(request, action, draftId, subject, await scribes.Exalt(draftId, note, scribe, cancellationToken)),
            "humble" => Answer(request, action, draftId, subject, await scribes.Humble(draftId, note, scribe, cancellationToken)),
            "restore" => Answer(request, action, draftId, subject, await scribes.Restore(draftId, note, scribe, Place(fields), cancellationToken)),
            "judge" => JudgedAgain(request, draftId, subject, await judgingAgain.JudgeAgain(draftId, scribe, cancellationToken)),
            "burn" => (await scribes.Burn(draftId, note, scribe, cancellationToken)).Match(
                // Nothing waits any more, or it can't be told: refill now rather than at the next daily run. A refill
                // only writes what's missing, so an unneeded one costs a read.
                Right: waiting =>
                {
                    logger.CountScribeAction(action);
                    return waiting.Match(
                        Some: count => count == 0
                            ? new ScriptoriumResponse { Result = AfterBurning("burn-last", draftId, subject), RefillReason = AllBurned }
                            : new ScriptoriumResponse { Result = AfterBurning("burn", draftId, subject) },
                        None: () => new ScriptoriumResponse { Result = AfterBurning("burn", draftId, subject), RefillReason = Uncounted });
                },
                Left: error => Failure(request, error, draftId, subject)),
            _ => new ScriptoriumResponse { Result = new NotFoundResult() },
        };
    }

    // Saved and judged again, back to the calendar; refused, the same page again with the Scribe's words and why.
    private async Task<ScriptoriumResponse> Amend(
        HttpRequest request, Guid draftId, IFormCollection fields, string scribe, CancellationToken cancellationToken)
    {
        var words = Words(fields);
        var note = fields["note"].ToString();
        var etag = fields["etag"].ToString();
        return await (await amendments.SaveAndJudgeAgain(draftId, etag, words, note, scribe, cancellationToken)).Match(
            Right: saved =>
            {
                logger.CountScribeAction("amend");
                return Task.FromResult(new ScriptoriumResponse { Result = Back(saved.JudgedAgain ? "amend" : "amend-unjudged", draftId, None) });
            },
            Left: error => IsTheScribes(error)
                ? AmendAgain(request, draftId, etag, words, None, note, error.Message, StatusCodes.Status422UnprocessableEntity, cancellationToken)
                : Task.FromResult(Failure(request, error, draftId, None)));
    }

    // The rewrite goes into the form, unsaved: the Scribe reads it before saving it.
    private async Task<ScriptoriumResponse> Rewrite(HttpRequest request, Guid draftId, IFormCollection fields, CancellationToken cancellationToken)
    {
        var words = Words(fields);
        var note = fields["note"].ToString();
        var etag = fields["etag"].ToString();
        return await (await amendments.ProposeRewrite(draftId, words, note, cancellationToken)).Match(
            Right: rewritten =>
            {
                logger.CountScribeAction("rewrite");
                return AmendAgain(request, draftId, etag, rewritten, Some(words), note,
                    "The Machine Spirit rewrote it as your note asks. Read it over, then save it, ask again, or leave it as it was.",
                    StatusCodes.Status200OK, cancellationToken);
            },
            Left: error =>
            {
                if (error == ScriptoriumRepository.ChangedMeanwhile)
                {
                    return Task.FromResult(new ScriptoriumResponse { Result = Back("changed", draftId, None) });
                }
                if (IsTheScribes(error))
                {
                    return AmendAgain(request, draftId, etag, words, None, note, error.Message, StatusCodes.Status422UnprocessableEntity, cancellationToken);
                }

                logger.LogError(error.ToException(), "The draft {DraftId} couldn't be rewritten: {Reason}", draftId, error.Message);
                return AmendAgain(request, draftId, etag, words, None, note,
                    "The Machine Spirit couldn't rewrite it just now. Try again in a moment, or amend it by hand.",
                    StatusCodes.Status503ServiceUnavailable, cancellationToken);
            });
    }

    // The amending page again, keeping the version the Scribe started from, so a change made meanwhile still stops the
    // save; gone from the calendar since, back to it to look again.
    private async Task<ScriptoriumResponse> AmendAgain(
        HttpRequest request, Guid draftId, string etag, RiteContent words, Option<RiteContent> rewrittenFrom, string note, string message, int status,
        CancellationToken cancellationToken)
        => (await amendments.FindWaiting(draftId, cancellationToken)).Match(
            Right: found => found.Match(
                Some: amendable => new ScriptoriumResponse
                {
                    Result = Html(request, ScriptoriumPage.RenderAmend(amendable with { ETag = etag }, words, note, Some(message), rewrittenFrom), status),
                },
                None: () => new ScriptoriumResponse { Result = Back("changed", draftId, None) }),
            Left: error => Failure(request, error, draftId, None));

    // The Scribe's to fix on the page: words breaking a rule, a note too long, or no note to rewrite by.
    private static bool IsTheScribes(Error error) => error is AmendmentRefused || error == Scribes.NoteTooLong;

    // A browser posts a textarea's line breaks as CRLF, while its maxlength counted each as one character: as LF, words
    // the field allowed fit the same limits here.
    private static RiteContent Words(IFormCollection fields)
        => new(fields["title"].ToString(), WithLineFeeds(fields["text"].ToString()), WithLineFeeds(fields["truth"].ToString()));

    private static string WithLineFeeds(string text) => text.Replace("\r\n", "\n");

    private static RiteContent Words(Draft draft) => new(draft.Writing.Title, draft.Writing.Text, draft.Writing.HereticalTruth);

    // Judged meanwhile (a refill did it), amended, burned or published: there's nothing left to judge, so look again.
    private ScriptoriumResponse JudgedAgain(HttpRequest request, Guid draftId, Option<Tag> subject, Either<Error, Option<Augury>> result)
        => result.Match(
            Right: augury =>
            {
                logger.CountScribeAction("judge");
                return new ScriptoriumResponse { Result = Back(augury.IsSome ? "judged" : "still-unjudged", draftId, subject) };
            },
            Left: error => error == JudgingAgain.NothingToJudge
                ? new ScriptoriumResponse { Result = Back("changed", draftId, subject) }
                : Failure(request, error, draftId, subject));

    private ScriptoriumResponse Answer(HttpRequest request, string action, Option<Guid> draftId, Option<Tag> subject, Either<Error, Unit> result)
        => result.Match(
            Right: _ =>
            {
                logger.CountScribeAction(action);
                return new ScriptoriumResponse { Result = Back(action, draftId, subject) };
            },
            Left: error => Failure(request, error, draftId, subject));

    private ScriptoriumResponse Failure(HttpRequest request, Error error, Option<Guid> draftId, Option<Tag> subject)
    {
        if (error == ScriptoriumRepository.ChangedMeanwhile)
        {
            return new ScriptoriumResponse { Result = Back("changed", draftId, subject) };
        }
        if (error == Scribes.NothingToMove)
        {
            return new ScriptoriumResponse { Result = Back("unmoved", draftId, subject) };
        }
        if (error == Scribes.NoteTooLong)
        {
            return new ScriptoriumResponse { Result = Back("note-too-long", draftId, subject) };
        }
        return new ScriptoriumResponse { Result = Failed(request, error) };
    }

    // The rite an address names, when it's burned and not among the rites the page shows anyway: on the calendar, the one
    // the last action burned, which the message names and links to; in the Ashes, one burned longer ago than those shown.
    // Unreadable, the page goes without it.
    private async Task<Option<Draft>> BurnedUnlessShown(Option<Guid> rite, Func<Guid, bool> shown, CancellationToken cancellationToken)
    {
        var gone = rite.Filter(id => !shown(id));
        return await gone.MatchAsync(
            Some: async id => (await scribes.FindBurned(id, cancellationToken)).IfLeft(Option<Draft>.None),
            None: () => Option<Draft>.None);
    }

    // The names are a nicety on these pages: unreadable, the Scriptorium still works, showing Scribes by their ids.
    private async Task<Option<NameInTheRecord>> NameOf(string scribe, CancellationToken cancellationToken)
    {
        return (await naming.NameOf(scribe, cancellationToken)).Match(
            Right: chosen => Some(new NameInTheRecord(chosen.IfNone(() => ScribeNames.Unnamed(scribe)), chosen.IsSome)),
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The Scribes' names couldn't be read: {Reason}", error.Message);
                return Option<NameInTheRecord>.None;
            });
    }

    private async Task<List<ScribeName>> Names(CancellationToken cancellationToken)
        => (await naming.All(cancellationToken)).Match(
            Right: all => all,
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The Scribes' names couldn't be read: {Reason}", error.Message);
                return [];
            });

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The Scriptorium failed: {Reason}", error.Message);
        return Html(request, ScriptoriumPage.RenderError(), StatusCodes.Status500InternalServerError);
    }

    private static IActionResult BackToPage(string outcome, Option<Tag> subject) => Redirect($"/scriptorium?done={outcome}{Narrowed(subject)}");

    // Back to the rite the Scribe acted on, where they were on the page, and the message names it.
    private static IActionResult Back(string outcome, Option<Guid> draftId, Option<Tag> subject)
        => draftId.Match(
            Some: id => Redirect($"/scriptorium?done={outcome}&rite={id:D}{Narrowed(subject)}#rite-{id:D}"),
            None: () => BackToPage(outcome, subject));

    // A burned rite has left the calendar, so the page stays at the message, which links to the rite in the Ashes.
    private static IActionResult AfterBurning(string outcome, Guid draftId, Option<Tag> subject)
        => Redirect($"/scriptorium?done={outcome}&rite={draftId:D}{Narrowed(subject)}");

    // The calendar narrowed to one subject stays narrowed after an action: its forms carry the subject.
    private static string Narrowed(Option<Tag> subject) => subject.Map(tag => $"&subject={tag.Key}").IfNone(string.Empty);

    // A key the vocabulary doesn't have is no filter.
    private static Option<Tag> Subject(string key) => RiteTags.Find(key);

    // Summon and Let the Augury Decide act on no one rite and need nothing else from their forms: one that can't be
    // read only loses the subject.
    private static async Task<Option<Tag>> SubjectOf(HttpRequest request, CancellationToken cancellationToken)
        => (await ReadForm(request, cancellationToken)).Bind(fields => Subject(fields["subject"].ToString()));

    // Where an Undo puts a burned rite back in the Scribes' order; none for a rite the Augury placed.
    private static Option<int> Place(IFormCollection fields)
        => int.TryParse(fields["place"].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var place) && place > 0 ? Some(place) : None;
}
