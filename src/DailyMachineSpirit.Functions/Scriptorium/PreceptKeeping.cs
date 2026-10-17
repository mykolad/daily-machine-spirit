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

    /// <summary>The code of a precept refused for breaking the writer's rules (#86): its message says which.</summary>
    public const int BreaksARule = 8601;

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

    public Task<Either<Error, Unit>> AddPinned(string text, string scribe, CancellationToken cancellationToken)
        => WithinTheRules(text, cancellationToken).BindAsync(words => Change(book => book.Precepts.Count >= PreceptBook.MaxPrecepts
            ? Left<Error, (PreceptBook, AuditEntry)>(Full)
            : (book with { Precepts = [.. book.Precepts, new Precept { Id = Guid.NewGuid(), Text = words, Pinned = true, ByScribe = true }] },
                Audit(scribe, AuditAction.AddPrecept, words, None)),
            None,
            cancellationToken));

    public Task<Either<Error, Unit>> EditAndPin(Guid preceptId, string etag, string text, string scribe, CancellationToken cancellationToken)
        => WithinTheRules(text, cancellationToken).BindAsync(words => Change(book => Find(book, preceptId).Map(found =>
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

    private static PreceptBook Replace(PreceptBook book, Precept changed)
        => book with { Precepts = book.Precepts.Select(precept => precept.Id == changed.Id ? changed : precept).ToList() };

    // The words, unless they'd have the writer break its rules. A check that can't answer lets them through, as judging
    // does: the writer still puts its rules first, and a model outage mustn't stop the Scribes.
    private Task<Either<Error, string>> WithinTheRules(string text, CancellationToken cancellationToken)
        => Text(text).BindAsync(async words => (await check.Breaches([words], cancellationToken)).Match(
            Right: breaches => breaches.HeadOrNone().Match(
                Some: breach => Left<Error, string>(Error.New(BreaksARule, breach.Reason)),
                None: () => Right<Error, string>(words)),
            Left: error =>
            {
                logger.LogWarning(error.ToException(), "A precept couldn't be checked against the rules, so it's saved unchecked: {Reason}", error.Message);
                return Right<Error, string>(words);
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
