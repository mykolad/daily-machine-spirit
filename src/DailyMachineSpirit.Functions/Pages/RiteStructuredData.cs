using System.Text.Json.Nodes;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Tags;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// A rite described for search engines (JSON-LD, schema.org's <c>CreativeWork</c>): its title, opening words, day and
/// subjects. The Heretical Truth stays out of it, as it stays out of the page's description: it's sealed on the page.
/// </summary>
public static class RiteStructuredData
{
    // The serializer's default escaping writes "<" as <, so nothing a model wrote can end the script element early.
    public static string Json(Rite rite, string url)
    {
        var data = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "CreativeWork",
            ["name"] = rite.Writing.Title,
            ["headline"] = rite.Writing.Title,
            ["description"] = Html.FirstLine(rite.Writing.Text.Replace("`", string.Empty)),
            ["genre"] = rite.Writing.Kind == RiteKind.Prayer ? "Prayer" : "Ritual",
            ["datePublished"] = Html.IsoDate(rite.PublishedOnUtc),
            ["url"] = url,
            ["inLanguage"] = "en",
            ["publisher"] = new JsonObject { ["@type"] = "Organization", ["name"] = Layout.SiteName },
        };
        var subjects = RiteTags.Of(rite.Tags);
        if (subjects.Count > 0)
        {
            data["keywords"] = string.Join(", ", subjects.Select(tag => tag.Label));
        }
        return data.ToJsonString();
    }
}
