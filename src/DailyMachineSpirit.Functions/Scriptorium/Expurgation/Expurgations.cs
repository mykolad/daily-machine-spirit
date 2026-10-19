using System.Security.Cryptography;
using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Pages;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>
/// What an Expurgator does: erase words from a published rite, as if lost to time. Each word becomes an emoji
/// everywhere the site keeps it (<see cref="IExpurgationRepository"/>): the rite, the draft it was published from, the
/// Scribes' decisions on it, the Record's entries about it, the drafts marked as much like it. The Record notes who
/// erased how many words of which part, never the words. Then Today, the rite's page and the search index are purged
/// from Cloudflare's cache.
/// </summary>
public sealed class Expurgations
{
    /// <summary>A rite's words changed since the Expurgator chose (another erasure): they choose again.</summary>
    public static readonly Error WordsChanged = Error.New("The rite's words changed since they were chosen.");

    public static readonly Error NothingChosen = Error.New("No word was chosen.");

    public static readonly Error NoSuchWord = Error.New("A chosen word isn't in the rite.");

    // A vote changes the rite's document too, so its version can change between reading and erasing while its words
    // stay the same: then it's read and erased again.
    private const int Tries = 3;

    private readonly IRiteRepository rites;
    private readonly IExpurgationRepository copies;
    private readonly IPageCache pages;
    private readonly TimeProvider time;

    public Expurgations(IRiteRepository rites, IExpurgationRepository copies, IPageCache pages, TimeProvider time)
    {
        this.rites = rites;
        this.copies = copies;
        this.pages = pages;
        this.time = time;
    }

    /// <summary>The newest published rites, to choose one from.</summary>
    public Task<Either<Error, List<Rite>>> Newest(int count, CancellationToken cancellationToken) => rites.GetNewest(count, cancellationToken);

    public Task<Either<Error, Option<ExpurgatableRite>>> Find(int number, CancellationToken cancellationToken)
        => rites.GetByNumbers([number], cancellationToken)
            .MapAsync(found => Task.FromResult(found.HeadOrNone().Map(rite => new ExpurgatableRite(rite, Version(rite.Writing)))));

    /// <summary>
    /// Erases the words: <see cref="WordsChanged"/> when the rite's words aren't the <paramref name="version"/> they were
    /// chosen from, None when there's no such rite. The rite as it reads now, else.
    /// </summary>
    public async Task<Either<Error, Option<Rite>>> Erase(
        int number, string version, IReadOnlyList<RedactedWord> chosen, string expurgator, CancellationToken cancellationToken)
    {
        if (chosen.Count == 0)
        {
            return NothingChosen;
        }

        var erased = await EraseOnce(number, version, chosen, expurgator, cancellationToken);
        for (var tried = 1; tried < Tries && erased.Match(Right: _ => false, Left: error => error == ScriptoriumRepository.ChangedMeanwhile); tried++)
        {
            erased = await EraseOnce(number, version, chosen, expurgator, cancellationToken);
        }
        if (erased.Exists(rite => rite.IsSome))
        {
            await pages.Purge([Paths.Today, Paths.Rite(number), Paths.SearchIndex], cancellationToken);
        }
        return erased;
    }

    /// <summary>The words' version: a hash of the three parts, which changes when any word does.</summary>
    public static string Version(RiteWriting writing)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\u0001', writing.Title, writing.Text, writing.HereticalTruth))))[..16].ToLowerInvariant();

    /// <summary>The rite's words with the chosen ones replaced, or <see cref="NoSuchWord"/> for a place it hasn't.</summary>
    public static Either<Error, RiteWriting> Redacted(RiteWriting writing, IReadOnlyList<RedactedWord> chosen)
    {
        var outside = chosen.Any(word => word.Word >= Wording.CountWords(word.Part.Of(writing)));
        if (outside)
        {
            return NoSuchWord;
        }
        return RiteParts.All.Aggregate(writing, (redacted, part) => redacted.With(part, Wording.Replace(
            part.Of(writing),
            chosen.Where(word => word.Part == part).DistinctBy(word => word.Word).ToDictionary(word => word.Word, word => word.Emoji))));
    }

    private Task<Either<Error, Option<Rite>>> EraseOnce(
        int number, string version, IReadOnlyList<RedactedWord> chosen, string expurgator, CancellationToken cancellationToken)
        => Find(number, cancellationToken).BindAsync(found => found.Match(
            None: () => Task.FromResult(Right<Error, Option<Rite>>(None)),
            Some: rite => rite.Version == version
                ? EraseFrom(rite.Rite, chosen, expurgator, cancellationToken)
                : Task.FromResult(Left<Error, Option<Rite>>(WordsChanged))));

    private async Task<Either<Error, Option<Rite>>> EraseFrom(
        Rite rite, IReadOnlyList<RedactedWord> chosen, string expurgator, CancellationToken cancellationToken)
    {
        if (Redacted(rite.Writing, chosen).Case is not RiteWriting redacted)
        {
            return NoSuchWord;
        }
        return await (await copies.FindWordsOf(rite.Number, cancellationToken)).Match(
            Left: error => Task.FromResult(Left<Error, Option<Rite>>(error)),
            Right: kept => kept.Match(
                // Gone between the two reads: there's nothing to erase.
                None: () => Task.FromResult(Right<Error, Option<Rite>>(None)),
                Some: async stored => (await copies.Erase(
                        Changed(stored, rite.Writing, redacted, WordsReplaced(rite.Writing, chosen)),
                        Entry(rite, redacted, chosen, expurgator),
                        cancellationToken))
                    .Map(_ => Some(rite with { Writing = redacted }))));
    }

    // Each copy of a part as it is now gets the redacted part; a copy from before an amendment, whose words differ, has
    // every erased word replaced wherever it appears: the Expurgator chose them in the rite, which is all they saw.
    private static List<WordsKept> Changed(
        IReadOnlyList<WordsKept> stored, RiteWriting original, RiteWriting redacted, IReadOnlyDictionary<string, string> words)
    {
        var parts = RiteParts.All.ToDictionary(part => part.Of(original), part => part.Of(redacted), StringComparer.Ordinal);
        return stored
            .Select(copy => copy with
            {
                Fields = copy.Fields
                    .Select(field => (field.Key, Was: field.Value, Now: parts.TryGetValue(field.Value, out var part) ? part : Wording.ReplaceEverywhere(field.Value, words)))
                    .Where(field => field.Now != field.Was)
                    .ToDictionary(field => field.Key, field => field.Now),
            })
            .Where(copy => copy.Fields.Count > 0)
            .ToList();
    }

    // Each erased word and the emoji it became; a word erased twice keeps the first.
    private static Dictionary<string, string> WordsReplaced(RiteWriting writing, IReadOnlyList<RedactedWord> chosen)
        => chosen
            .Select(word => (Text: Wording.Split(word.Part.Of(writing)).Where(piece => piece.IsWord).ElementAt(word.Word).Text, word.Emoji))
            .DistinctBy(word => word.Text)
            .ToDictionary(word => word.Text, word => word.Emoji, StringComparer.Ordinal);

    // Who, and how many words of which parts: never the words.
    private AuditEntry Entry(Rite rite, RiteWriting redacted, IReadOnlyList<RedactedWord> chosen, string expurgator) => new()
    {
        Id = Guid.NewGuid(),
        AtUtc = time.GetUtcNow().UtcDateTime,
        Actor = expurgator,
        Action = AuditAction.Expurgate,
        RiteNumber = rite.Number,
        Title = redacted.Title,
        Detail = string.Join(", ", RiteParts.All
            .Select(part => (Part: part, Words: chosen.Where(word => word.Part == part).Select(word => word.Word).Distinct().Count()))
            .Where(counted => counted.Words > 0)
            .Select(counted => $"{counted.Words} {(counted.Words == 1 ? "word" : "words")} of its {counted.Part.Name()}")),
    };
}
