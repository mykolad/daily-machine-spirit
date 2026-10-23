using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Precepts;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Scribes have the final say over what the Machine Spirit learned (#12): they add, edit, pin and delete precepts.
/// A Scribe's words are pinned, so distilling again never replaces them; a deleted precept is remembered, so it isn't
/// proposed again. Each change is recorded (#9) and guarded by the Precepts' ETag.
/// </summary>
public sealed class PreceptKeeping
{
    public static readonly Error TextNeeded = Error.New("A precept needs words.");

    public static readonly Error TooLong = Error.New($"A precept can be at most {Precept.MaxLength} characters.");

    public static readonly Error Full = Error.New($"There are {PreceptBook.MaxPrecepts} precepts already: delete or edit one instead.");

    public static readonly Error Unchecked = Error.New("The rules couldn't be checked just now.");

    /// <summary>What the Record says of a precept an Undo put back: added again, where it was.</summary>
    public const string PutBack = "put back after a delete";

    private readonly IPreceptRepository precepts;
    private readonly PreceptCheck check;
    private readonly TimeProvider time;
    private readonly ILogger<PreceptKeeping> logger;

    public PreceptKeeping(IPreceptRepository precepts, PreceptCheck check, TimeProvider time, ILogger<PreceptKeeping> logger)
    {
        this.precepts = precepts;
        this.check = check;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>The Precepts, and the version the page shows: its forms send it back, so a change from a page shown
    /// before another change is refused.</summary>
    public Task<Either<Error, StoredPrecepts>> View(CancellationToken cancellationToken) => precepts.Get(cancellationToken);

    /// <summary>The new precept's id.</summary>
    public Task<Either<Error, Guid>> AddPinned(string text, string scribe, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        return WithinTheRules(text, cancellationToken).BindAsync(words => Change(book => book.Precepts.Count >= PreceptBook.MaxPrecepts
            ? Left<Error, (PreceptBook, AuditEntry)>(Full)
            : (book with { Precepts = [.. book.Precepts, new Precept { Id = id, Text = words, Pinned = true, Origin = PreceptOrigin.Written, By = scribe }] },
                Audit(scribe, AuditAction.AddPrecept, words, None)),
            None,
            cancellationToken)).MapAsync(_ => Task.FromResult(id));
    }

    public Task<Either<Error, Unit>> EditAndPin(Guid preceptId, string etag, string text, string scribe, CancellationToken cancellationToken)
        => WithinTheRules(text, cancellationToken).BindAsync(words => Change(book => Find(book, preceptId).Map(found =>
            (Replace(book, found with { Text = words, Pinned = true, Origin = PreceptOrigin.Amended, By = scribe }),
                Audit(scribe, AuditAction.EditPrecept, words, Some($"was “{found.Text}”")))),
            Some(etag),
            cancellationToken));

    // A Scribe's words keep saying whose they are; a distilled precept says who pinned it, until it's unpinned.
    public Task<Either<Error, Unit>> Pin(Guid preceptId, string etag, string scribe, CancellationToken cancellationToken)
        => Change(book => Find(book, preceptId).Map(found =>
            (Replace(book, found with { Pinned = true, By = found.ByScribe ? found.By : scribe }),
                Audit(scribe, AuditAction.PinPrecept, found.Text, None))),
            Some(etag),
            cancellationToken);

    public Task<Either<Error, Unit>> Unpin(Guid preceptId, string etag, string scribe, CancellationToken cancellationToken)
        => Change(book => Find(book, preceptId).Map(found =>
            (Replace(book, found with { Pinned = false, By = found.ByScribe ? found.By : None }),
                Audit(scribe, AuditAction.UnpinPrecept, found.Text, None))),
            Some(etag),
            cancellationToken);

    /// <summary>
    /// Deletes the precept, and remembers its words so a distillation doesn't propose them again. It's kept, with its
    /// place, for <see cref="RestoreDeleted"/>.
    /// </summary>
    public Task<Either<Error, Unit>> Delete(Guid preceptId, string etag, string scribe, CancellationToken cancellationToken)
        => Change(book => Find(book, preceptId).Map(found =>
            (book with
            {
                Precepts = book.Precepts.Where(precept => precept.Id != preceptId).ToList(),
                Rejected = [found.Text, .. book.Rejected.Take(PreceptBook.RejectedKept - 1)],
                // The rejection a full list let go of, to be remembered again if the delete is undone.
                LastDeleted = new DeletedPrecept(found, book.Precepts.IndexOf(found),
                    book.Rejected.Count >= PreceptBook.RejectedKept ? Optional(book.Rejected[PreceptBook.RejectedKept - 1]) : None),
            },
                Audit(scribe, AuditAction.DeletePrecept, found.Text, None))),
            Some(etag),
            cancellationToken);

    /// <summary>
    /// Undoes the last delete: the precept goes back where it was, as it was, and a distillation may propose its words
    /// again. Only straight after the delete, on the version it left: any change since is
    /// <see cref="ScriptoriumRepository.ChangedMeanwhile"/>.
    /// </summary>
    public Task<Either<Error, Unit>> RestoreDeleted(Guid preceptId, string etag, string scribe, CancellationToken cancellationToken)
        => precepts.Get(cancellationToken)
            .BindAsync(read => Seen(read, Some(etag))
                .Bind(book => book.LastDeleted.Filter(deleted => deleted.Precept.Id == preceptId).ToEither(ScriptoriumRepository.ChangedMeanwhile)
                    .Bind(deleted => book.Precepts.Count >= PreceptBook.MaxPrecepts
                        ? Left<Error, (PreceptBook, AuditEntry)>(Full)
                        : (book with
                        {
                            Precepts = [.. book.Precepts.Take(deleted.Place), deleted.Precept, .. book.Precepts.Skip(deleted.Place)],
                            Rejected = [.. WithoutFirst(book.Rejected, deleted.Precept.Text), .. deleted.LetGo],
                            LastDeleted = None,
                        },
                            // Recorded as an addition, which any version of the app reads: a rollback mustn't break the Record.
                            Audit(scribe, AuditAction.AddPrecept, deleted.Precept.Text, Some(PutBack)))))
                .BindAsync(changed => precepts.Save(read, PinnedFirst(changed.Item1), changed.Item2, cancellationToken)));

    // Any other change forgets the last delete: an Undo is offered only straight after it.
    private Task<Either<Error, Unit>> Change(
        Func<PreceptBook, Either<Error, (PreceptBook Changed, AuditEntry Audit)>> change,
        Option<string> seen,
        CancellationToken cancellationToken)
        => precepts.Get(cancellationToken)
            .BindAsync(read => Seen(read, seen).Map(book => book with { LastDeleted = None }).Bind(change)
                .BindAsync(changed => precepts.Save(read, PinnedFirst(changed.Changed), changed.Audit, cancellationToken)));

    // A change to a precept is made on the version the Scribe saw: changed since (another Scribe's edit, a distillation),
    // they look again rather than overwrite it. Adding one changes nobody's words, so it needs no version.
    private static Either<Error, PreceptBook> Seen(StoredPrecepts read, Option<string> seen)
        => seen.ForAll(etag => read.ETag.Exists(current => current == etag))
            ? read.Book
            : ScriptoriumRepository.ChangedMeanwhile;

    // The pinned lead, each group in the order it had: pinning a precept moves it to the end of the pinned, unpinning
    // to the start of the rest, and a Scribe's new one joins the pinned. A distillation keeps the same order.
    private static PreceptBook PinnedFirst(PreceptBook book)
        => book with { Precepts = book.Precepts.OrderBy(precept => !precept.Pinned).ToList() };

    // Gone meanwhile: a distillation replaced it, or another Scribe deleted it.
    private static Either<Error, Precept> Find(PreceptBook book, Guid preceptId)
        => book.Precepts.Where(precept => precept.Id == preceptId).HeadOrNone().ToEither(ScriptoriumRepository.ChangedMeanwhile);

    private static List<string> WithoutFirst(List<string> texts, string text)
    {
        var index = texts.IndexOf(text);
        return index < 0 ? texts : [.. texts[..index], .. texts[(index + 1)..]];
    }

    private static PreceptBook Replace(PreceptBook book, Precept changed)
        => book with { Precepts = book.Precepts.Select(precept => precept.Id == changed.Id ? changed : precept).ToList() };

    // The words, once the check says they keep the writer within its rules. Unchecked, they aren't saved: the writer would
    // follow them in every rite, and nothing would check them later.
    private Task<Either<Error, string>> WithinTheRules(string text, CancellationToken cancellationToken)
        => Text(text).BindAsync(async words => (await check.Breaches([words], cancellationToken)).Match(
            Right: breaches => breaches.HeadOrNone().Match(
                Some: breach => Left<Error, string>(new BreaksARule(breach.Reason)),
                None: () => Right<Error, string>(words)),
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "A precept couldn't be checked against the rules, so it isn't saved: {Reason}", error.Message);
                return Left<Error, string>(Unchecked);
            }));

    private static Either<Error, string> Text(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return TextNeeded;
        }
        if (trimmed.Length > Precept.MaxLength)
        {
            return TooLong;
        }
        return trimmed;
    }

    private AuditEntry Audit(string scribe, AuditAction action, string text, Option<string> detail) => new()
    {
        Id = Guid.NewGuid(),
        AtUtc = time.GetUtcNow().UtcDateTime,
        Actor = scribe,
        Action = action,
        Title = text,
        Detail = detail,
    };
}
