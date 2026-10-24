using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Generation.Writing;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// A Scribe amends a waiting draft's words (#8): by hand, or by asking the writer to rewrite them as a note says. The
/// words must pass what a model's answer must (<see cref="RiteChecks"/>), and the amended draft is judged again, so its
/// scores, quality and tags describe what will be published. It keeps the model that wrote it first, and says it was
/// edited, never by which Scribe. Each amendment is a decision too, with the words as they were.
/// </summary>
public sealed class Amendments
{
    public static readonly Error NoteNeeded = new AmendmentRefused("Write a note first: it tells the Machine Spirit what to change.");

    private readonly IDraftRepository drafts;
    private readonly IScriptoriumRepository scriptorium;
    private readonly Scribes scribes;
    private readonly IRiteJudge judge;
    private readonly RiteWriter writer;
    private readonly TimeProvider time;
    private readonly ILogger<Amendments> logger;

    public Amendments(
        IDraftRepository drafts,
        IScriptoriumRepository scriptorium,
        Scribes scribes,
        IRiteJudge judge,
        RiteWriter writer,
        TimeProvider time,
        ILogger<Amendments> logger)
    {
        this.drafts = drafts;
        this.scribes = scribes;
        this.scriptorium = scriptorium;
        this.judge = judge;
        this.writer = writer;
        this.time = time;
        this.logger = logger;
    }

    public Task<Either<Error, Option<AmendableDraft>>> FindWaiting(Guid draftId, CancellationToken cancellationToken)
        => drafts.GetWithETag(draftId, cancellationToken)
            .MapAsync(found => Task.FromResult(found
                .Filter(stored => stored.Draft.State == DraftState.Waiting)
                .Map(stored => new AmendableDraft(stored.Draft, stored.ETag))));

    /// <param name="etag">The version the Scribe read: changed since, nothing is saved.</param>
    public Task<Either<Error, SavedAmendment>> SaveAndJudgeAgain(
        Guid draftId, string etag, RiteContent words, string note, ScribeId scribe, CancellationToken cancellationToken)
        => CheckedNote(note).BindAsync(checkedNote => AsTheScribeRead(draftId, etag, cancellationToken)
            .BindAsync(draft => RiteChecks.Check(words, draft.Writing.Kind).MapLeft(Refusal)
                .BindAsync(checkedWords => scribes.PlacesOf(draft.Id, cancellationToken)
                    .MapAsync(async places =>
                        (Judged: await JudgedAgain(WithNewWords(draft, checkedWords), cancellationToken), Places: places)))
                .BindAsync(amended => scriptorium
                    .Amend(amended.Judged.Draft, etag, Decision(draft, checkedNote, amended.Places),
                        Audit(scribe, draft, amended.Judged.Draft), cancellationToken)
                    .MapAsync(_ => Task.FromResult(new SavedAmendment(amended.Judged.Judged))))));

    public Task<Either<Error, RiteContent>> ProposeRewrite(Guid draftId, RiteContent words, string note, CancellationToken cancellationToken)
        => CheckedNote(note).Bind(checkedNote => checkedNote.ToEither(NoteNeeded))
            .BindAsync(checkedNote => FindWaiting(draftId, cancellationToken)
                .BindAsync(found => found.Match(
                    Some: amendable => writer.Rewrite(amendable.Draft.Writing.Kind, words, checkedNote, cancellationToken),
                    None: () => Task.FromResult(Left<Error, RiteContent>(ScriptoriumRepository.ChangedMeanwhile)))));

    // The draft as the Scribe read it: gone, burned, published or amended since, it's a change to look at again.
    private Task<Either<Error, Draft>> AsTheScribeRead(Guid draftId, string etag, CancellationToken cancellationToken)
        => FindWaiting(draftId, cancellationToken)
            .BindAsync(found => Task.FromResult(found
                .Filter(amendable => amendable.ETag == etag)
                .Map(amendable => amendable.Draft)
                .ToEither(ScriptoriumRepository.ChangedMeanwhile)));

    // Its old scores, quality, tags and likeness to another rite describe the old words, so they go; judging gives the new
    // ones, and the Scribe who amended it has read it.
    private static Draft WithNewWords(Draft draft, RiteContent words) => draft with
    {
        Writing = draft.Writing with { Title = words.Title, Text = words.Text, HereticalTruth = words.HereticalTruth, Ailment = Some(words.Ailment), Edited = true },
        Similarity = None,
        Augury = None,
        Tags = None,
        Resembles = None,
    };

    // A judge that fails, or is off, doesn't lose the Scribe's words: the draft waits unjudged, and the page says so.
    private async Task<(Draft Draft, bool Judged)> JudgedAgain(Draft amended, CancellationToken cancellationToken)
        => (await judge.Judge(amended, cancellationToken)).Match(
            // A judge that's turned off answers the draft as it was: with its quality cleared, it's still unjudged.
            Right: judged => (judged, judged.Augury.IsSome),
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The amended draft {DraftId} couldn't be judged again: {Reason}", amended.Id, error.Message);
                return (amended, false);
            });

    // The words and places as they were before the amendment, read before it was judged again.
    private ScribeDecision Decision(Draft before, Option<string> note, Option<DraftPlaces> places) => new()
    {
        Id = Guid.NewGuid(),
        DraftId = before.Id,
        Action = ScribeAction.Amend,
        Kind = before.Writing.Kind,
        Title = before.Writing.Title,
        Text = before.Writing.Text,
        HereticalTruth = before.Writing.HereticalTruth,
        Ailment = before.Writing.Ailment,
        CalendarPlace = places.Map(found => found.CalendarPlace),
        AuguryPlace = places.Map(found => found.AuguryPlace),
        AuguryRankedByQuality = places.Exists(found => found.AuguryRankedByQuality),
        AuguryQuality = before.Augury.Map(augury => augury.Quality),
        Note = note,
        DecidedAtUtc = time.GetUtcNow().UtcDateTime,
    };

    // The title it had before, when that changed: the record's line names the rite by its new one.
    private AuditEntry Audit(ScribeId scribe, Draft before, Draft after)
        => AuditEntries.Of(time, scribe, AuditAction.Amend, after, before.Writing.Title == after.Writing.Title
            ? None
            : Some($"was “{before.Writing.Title}”"));

    private static Error Refusal(Error why) => new AmendmentRefused(why.Message);

    // An empty note is no note, as with every other decision.
    private static Either<Error, Option<string>> CheckedNote(string note)
    {
        var trimmed = note.Trim();
        if (trimmed.Length > ScribeDecision.MaxNoteLength)
        {
            return Scribes.NoteTooLong;
        }
        return trimmed.Length == 0 ? Option<string>.None : Some(trimmed);
    }
}
