using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
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

    private readonly IPreceptRepository precepts;
    private readonly TimeProvider time;

    public PreceptKeeping(IPreceptRepository precepts, TimeProvider time)
    {
        this.precepts = precepts;
        this.time = time;
    }

    /// <summary>The Precepts, and the version the page shows: its forms send it back, so a change from a page shown
    /// before another change is refused.</summary>
    public Task<Either<Error, StoredPrecepts>> View(CancellationToken cancellationToken) => precepts.Get(cancellationToken);

    public Task<Either<Error, Unit>> Add(string text, string scribe, CancellationToken cancellationToken)
        => Text(text).BindAsync(words => Change(book => book.Precepts.Count >= PreceptBook.MaxPrecepts
            ? Left<Error, (PreceptBook, AuditEntry)>(Full)
            : (book with { Precepts = [.. book.Precepts, new Precept { Id = Guid.NewGuid(), Text = words, Pinned = true, ByScribe = true }] },
                Audit(scribe, AuditAction.AddPrecept, words, None)),
            None,
            cancellationToken));

    /// <summary>New words for the precept, pinned: a Scribe's words aren't for a model to replace.</summary>
    public Task<Either<Error, Unit>> Edit(Guid preceptId, string etag, string text, string scribe, CancellationToken cancellationToken)
        => Text(text).BindAsync(words => Change(book => Find(book, preceptId).Map(found =>
            (Replace(book, found with { Text = words, Pinned = true, ByScribe = true }),
                Audit(scribe, AuditAction.EditPrecept, words, Some($"was “{found.Text}”")))),
            Some(etag),
            cancellationToken));

    public Task<Either<Error, Unit>> Pin(Guid preceptId, string etag, string scribe, CancellationToken cancellationToken)
        => Change(book => Find(book, preceptId).Map(found =>
            (Replace(book, found with { Pinned = true }), Audit(scribe, AuditAction.PinPrecept, found.Text, None))),
            Some(etag),
            cancellationToken);

    public Task<Either<Error, Unit>> Unpin(Guid preceptId, string etag, string scribe, CancellationToken cancellationToken)
        => Change(book => Find(book, preceptId).Map(found =>
            (Replace(book, found with { Pinned = false }), Audit(scribe, AuditAction.UnpinPrecept, found.Text, None))),
            Some(etag),
            cancellationToken);

    /// <summary>Deletes the precept, and remembers its words so a distillation doesn't propose them again.</summary>
    public Task<Either<Error, Unit>> Delete(Guid preceptId, string etag, string scribe, CancellationToken cancellationToken)
        => Change(book => Find(book, preceptId).Map(found =>
            (book with
            {
                Precepts = book.Precepts.Where(precept => precept.Id != preceptId).ToList(),
                Rejected = [found.Text, .. book.Rejected.Take(PreceptBook.RejectedKept - 1)],
            },
                Audit(scribe, AuditAction.DeletePrecept, found.Text, None))),
            Some(etag),
            cancellationToken);

    private Task<Either<Error, Unit>> Change(
        Func<PreceptBook, Either<Error, (PreceptBook Changed, AuditEntry Audit)>> change,
        Option<string> seen,
        CancellationToken cancellationToken)
        => precepts.Get(cancellationToken)
            .BindAsync(read => Seen(read, seen).Bind(change)
                .BindAsync(changed => precepts.Save(read, changed.Changed, changed.Audit, cancellationToken)));

    // A change to a precept is made on the version the Scribe saw: changed since (another Scribe's edit, a distillation),
    // they look again rather than overwrite it. Adding one changes nobody's words, so it needs no version.
    private static Either<Error, PreceptBook> Seen(StoredPrecepts read, Option<string> seen)
        => seen.ForAll(etag => read.ETag.Exists(current => current == etag))
            ? read.Book
            : ScriptoriumRepository.ChangedMeanwhile;

    // Gone meanwhile: a distillation replaced it, or another Scribe deleted it.
    private static Either<Error, Precept> Find(PreceptBook book, Guid preceptId)
        => book.Precepts.Where(precept => precept.Id == preceptId).HeadOrNone().ToEither(ScriptoriumRepository.ChangedMeanwhile);

    private static PreceptBook Replace(PreceptBook book, Precept changed)
        => book with { Precepts = book.Precepts.Select(precept => precept.Id == changed.Id ? changed : precept).ToList() };

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
