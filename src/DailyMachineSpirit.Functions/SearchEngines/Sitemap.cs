using System.Xml.Linq;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Tags;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.SearchEngines;

/// <summary>
/// Every public page a search engine should know (sitemaps.org): Today, the Archive, the Reliquary, the Catechism, every
/// subject's page and every rite's, each rite with its day. Numbers and days only, so an erasure, which changes words,
/// leaves it as it was.
/// </summary>
public static class Sitemap
{
    private static readonly XNamespace Urls = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <param name="days">Every published rite's number and day, in any order.</param>
    public static string Render(string origin, IReadOnlyList<RiteDay> days)
    {
        var newestFirst = days.OrderByDescending(day => day.Number).ToList();
        // Today and the Archive's first page change with each new rite. The other pages change with votes or a
        // deployment, on days nobody records, so they give none.
        var newest = newestFirst.HeadOrNone().Map(day => day.PublishedOnUtc);
        var undated = new[] { Paths.Reliquary, Paths.ReliquaryConfessed, Paths.About }.Concat(RiteTags.All.Select(tag => Paths.Tag(tag.Key)));
        var urls = new[] { Url(origin, Paths.Today, newest), Url(origin, Paths.Archive, newest) }
            .Concat(undated.Select(path => Url(origin, path, None)))
            .Concat(newestFirst.Select(day => Url(origin, Paths.Rite(day.Number), Some(day.PublishedOnUtc))));
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{new XElement(Urls + "urlset", urls).ToString(SaveOptions.DisableFormatting)}";
    }

    private static XElement Url(string origin, string path, Option<DateOnly> changed)
        => new(Urls + "url",
            new XElement(Urls + "loc", origin + path),
            changed.Map(day => new XElement(Urls + "lastmod", Html.IsoDate(day))).AsEnumerable());
}
