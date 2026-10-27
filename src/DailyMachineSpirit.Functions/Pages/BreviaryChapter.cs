using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// A chapter of the reader's Breviary: the rites of one subject, so a rite is found by what it's about rather than by
/// when it was published. Its rubric over the rows, and its line in the chapters' index.
/// </summary>
public sealed record BreviaryChapter(Tag Subject, IReadOnlyList<Rite> Rites)
{
    private static readonly Tag Unfiled = new("unfiled", "Unfiled", string.Empty);

    /// <summary>"chapter-caches": the rubric's id.</summary>
    public string Anchor => $"chapter-{Subject.Key}";

    /// <summary>
    /// The chapters these rites fall in, by name, as a book's are, with the rites without a subject last.
    /// </summary>
    /// <param name="rites">The rites, in the order each chapter keeps them.</param>
    public static List<BreviaryChapter> Of(IReadOnlyList<Rite> rites)
        => rites
            .GroupBy(SubjectOf)
            .OrderBy(chapter => chapter.Key == Unfiled)
            .ThenBy(chapter => chapter.Key.Label, StringComparer.Ordinal)
            .Select(chapter => new BreviaryChapter(chapter.Key, chapter.ToList()))
            .ToList();

    /// <summary>
    /// The chapter a rite is in. A rite with two subjects is in its likeliest's alone (the first its judge gave): the same
    /// rite twice on a page reads as two.
    /// </summary>
    public static Tag SubjectOf(Rite rite) => RiteTags.Of(rite.Tags).HeadOrNone().IfNone(Unfiled);

    /// <summary>The chapter a link names (<c>?chapter=caches</c>), or None for a key that's no subject's.</summary>
    public static Option<Tag> Named(string key) => key == Unfiled.Key ? Some(Unfiled) : RiteTags.Find(key);
}
