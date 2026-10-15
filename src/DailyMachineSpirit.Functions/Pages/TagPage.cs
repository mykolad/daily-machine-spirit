using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A tag's rites (<c>/tags/&lt;key&gt;</c>), newest first, a page at a time like the archive; then every other tag.</summary>
public static class TagPage
{
    /// <param name="olderThan">Where the next page starts, or None when these are the oldest.</param>
    /// <param name="isFirstPage">An empty first page means the tag has no rites yet; an empty later one, only that
    /// none are older.</param>
    public static string Render(Tag tag, IReadOnlyList<Rite> rites, Option<int> olderThan, bool isFirstPage)
    {
        var label = Html.Encode(tag.Label);
        var about = $"Rites about {tag.About}, newest first.";
        var end = isFirstPage && rites.Count == 0
            ? $"No rite is filed under {label} yet."
            : $"No earlier rite is filed under {label}.";
        var others = RiteTags.All.Where(other => other != tag);
        return Layout.Page($"Filed under {tag.Label}", about, Section.Other, $"""
            {AltarHeading.Render($"Filed under {label}", Html.Encode(about))}
            {RiteRows.Render(rites, olderThan.Map(number => Paths.TagBefore(tag.Key, number)), end)}
            <section class="subjects" aria-labelledby="subjects">
            <h2 id="subjects">Other subjects</h2>
            <p class="tags">{TagLinks.Render(others)}</p>
            </section>
            """);
    }
}
