using DailyMachineSpirit.Functions.Tags;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>A rite's tags as links to their pages. They sit on the dark background, never on the parchment.</summary>
public static class TagLinks
{
    public static string Render(IEnumerable<Tag> tags)
        => string.Join(" ", tags.Select(tag => $"""<a class="tag" href="{Paths.Tag(tag.Key)}">{Html.Encode(tag.Label)}</a>"""));
}
