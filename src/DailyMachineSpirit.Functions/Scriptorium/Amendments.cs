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
    /// <summary>The words broke a rule every rite keeps: the error's message says which, for the Scribe to fix.</summary>
    public const int Refused = 8001;

    public static readonly Error NoteNeeded = Error.New(Refused, "Write a note first: it tells the Machine Spirit what to change.");

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

    /// <summary>The draft, if it's still waiting: a burned or published one can't be amended.</summary>
    public Task<Either<Error, Option<AmendableDraft>>> Get(Guid draftId, CancellationToken cancellationToken)
        => drafts.GetWithETag(draftId, cancellationToken)
            .MapAsync(found => Task.FromResult(found
                .Filter(stored => stored.Draft.State == DraftState.Waiting)
                .Map(stored => new AmendableDraft(stored.Draft, stored.ETag))));

    /// <summary>
    /// Saves the words if the draft is still the version the Scribe read (<paramref name="etag"/>): true when it was
    /// judged again, false when the judge failed and it waits without scores, as a draft that couldn't be judged does.
    /// </summary>
    public Task<Either<Error, bool>> Save(Guid draftId, string etag, RiteContent words, string note, CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => Current(draftId, etag, cancellationToken)
            .BindAsync(draft => RiteChecks.Check(words, draft.Writing.Kind).MapLeft(Refusal)
                .BindAsync(checkedWords => scribes.PlacesOf(draft.Id, cancellationToken)
                    .MapAsync(async places =>
                        (Judged: await Judged(Amended(draft, checkedWords), cancellationToken), Places: places)))
                .BindAsync(amended => scriptorium
                    .Amend(amended.Judged.Draft, etag, Decision(draft, checkedNote, amended.Places), cancellationToken)
                    .MapAsync(_ => Task.FromResult(amended.Judged.Judged)))));

    /// <summary>The words rewritten by the writer as the note asks; not saved, for the Scribe to read first.</summary>
    public Task<Either<Error, RiteContent>> Rewrite(Guid draftId, RiteContent words, string note, CancellationToken cancellationToken)
        => Note(note).Bind(checkedNote => checkedNote.ToEither(NoteNeeded))
            .BindAsync(checkedNote => Get(draftId, cancellationToken)
                .BindAsync(found => found.Match(
                    Some: amendable => writer.Rewrite(amendable.Draft.Writing.Kind, words, checkedNote, cancellationToken),
                    None: () => Task.FromResult(Left<Error, RiteContent>(ScriptoriumRepository.ChangedMeanwhile)))));

    // The draft as the Scribe read it: gone, burned, published or amended since, it's a change to look at again.
    private Task<Either<Error, Draft>> Current(Guid draftId, string etag, CancellationToken cancellationToken)
        => Get(draftId, cancellationToken)
            .BindAsync(found => Task.FromResult(found
                .Filter(amendable => amendable.ETag == etag)
                .Map(amendable => amendable.Draft)
                .ToEither(ScriptoriumRepository.ChangedMeanwhile)));

    // Its old scores, quality and tags describe the old words, so they go; judging gives the new ones.
    private static Draft Amended(Draft draft, RiteContent words) => draft with
    {
        Writing = draft.Writing with { Title = words.Title, Text = words.Text, HereticalTruth = words.HereticalTruth, Edited = true },
        Similarity = None,
        Augury = None,
        Tags = None,
    };

    // A judge that fails, or is off, doesn't lose the Scribe's words: the draft waits unjudged, and the page says so.
    private async Task<(Draft Draft, bool Judged)> Judged(Draft amended, CancellationToken cancellationToken)
        => (await judge.Judge(amended, cancellationToken)).Match(
            // A judge that's turned off answers the draft as it was: with its quality cleared, it's still unjudged.
            Right: judged => (judged, judged.Augury.IsSome),
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "The amended draft {DraftId} couldn't be judged again: {Reason}", amended.Id, error.Message);
                return (amended, false);
            });

    // The words and places as they were before the amendment, read before it was judged again.
    private ScribeDecision Decision(Draft before, Option<string> note, Option<(int CalendarPlace, int AuguryPlace)> places) => new()
    {
        Id = Guid.NewGuid(),
        DraftId = before.Id,
        Action = ScribeAction.Amend,
        Kind = before.Writing.Kind,
        Title = before.Writing.Title,
        Text = before.Writing.Text,
        HereticalTruth = before.Writing.HereticalTruth,
        CalendarPlace = places.Map(found => found.CalendarPlace),
        AuguryPlace = places.Map(found => found.AuguryPlace),
        AuguryQuality = before.Augury.Map(augury => augury.Quality),
        Note = note,
        DecidedAtUtc = time.GetUtcNow().UtcDateTime,
    };

    private static Error Refusal(Error why) => Error.New(Refused, why.Message);

    // An empty note is no note, as with every other decision.
    private static Either<Error, Option<string>> Note(string note)
    {
        var trimmed = note.Trim();
        if (trimmed.Length > ScribeDecision.MaxNoteLength)
        {
            return Scribes.NoteTooLong;
        }
        return trimmed.Length == 0 ? Option<string>.None : Some(trimmed);
    }
}
