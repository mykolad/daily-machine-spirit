using System.Globalization;
using System.Text;
using System.Xml.Linq;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Tags;

namespace DailyMachineSpirit.Functions.Feeds;

/// <summary>
/// The newest rites as an Atom feed (RFC 4287): each entry the rite's title, day, kind and subjects, and its text, ending
/// with a link to break the seal on its page. The Heretical Truth stays sealed, as on the page.
/// </summary>
public static class AtomFeed
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    /// <param name="newestFirst">The rites to list, newest first.</param>
    /// <param name="now">The feed's time when it lists no rite yet; otherwise it's its latest entry's.</param>
    public static string Render(string origin, IReadOnlyList<Rite> newestFirst, DateTimeOffset now)
    {
        var updated = newestFirst.Count == 0 ? now : newestFirst.Max(Updated);
        var feed = new XElement(Atom + "feed",
            new XElement(Atom + "title", Layout.SiteName),
            new XElement(Atom + "subtitle", Masthead.Tagline),
            new XElement(Atom + "id", origin + Paths.Today),
            new XElement(Atom + "link", new XAttribute("rel", "self"), new XAttribute("type", "application/atom+xml"), new XAttribute("href", origin + Paths.Feed)),
            new XElement(Atom + "link", new XAttribute("rel", "alternate"), new XAttribute("type", "text/html"), new XAttribute("href", origin + Paths.Today)),
            new XElement(Atom + "updated", Timestamp(updated)),
            new XElement(Atom + "author", new XElement(Atom + "name", Layout.SiteName)),
            newestFirst.Select(rite => Entry(origin, rite)));
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{feed.ToString(SaveOptions.DisableFormatting)}";
    }

    // The text as the page shows it: HTML-encoded, code in <code>. XLinq then escapes that HTML once more for XML, so a
    // reader decodes it to the HTML it renders, and nothing a model wrote is ever markup.
    private static XElement Entry(string origin, Rite rite)
    {
        var page = origin + Paths.Rite(rite.Number);
        var day = Timestamp(Midnight(rite.PublishedOnUtc));
        var updated = Timestamp(Updated(rite));
        var (kind, kindLabel) = rite.Writing.Kind == RiteKind.Prayer ? ("prayer", "Prayer") : ("ritual", "Ritual");
        var content = $"""<p>{Html.WithInlineCode(XmlSafe(rite.Writing.Text))}</p><p>The Heretical Truth is sealed. Break the seal: <a href="{Html.Encode(page)}">{Html.Encode(page)}</a></p>""";
        return new XElement(Atom + "entry",
            new XElement(Atom + "title", XmlSafe(rite.Writing.Title)),
            new XElement(Atom + "id", page),
            new XElement(Atom + "link", new XAttribute("rel", "alternate"), new XAttribute("type", "text/html"), new XAttribute("href", page)),
            new XElement(Atom + "published", day),
            new XElement(Atom + "updated", updated),
            new XElement(Atom + "category", new XAttribute("term", kind), new XAttribute("label", kindLabel)),
            RiteTags.Of(rite.Tags).Select(tag => new XElement(Atom + "category", new XAttribute("term", tag.Key), new XAttribute("label", tag.Label))),
            new XElement(Atom + "content", new XAttribute("type", "html"), content));
    }

    // A rite's words change only when an Expurgator erases some: then its entry is updated, so a reader that kept the
    // old words replaces them.
    private static DateTimeOffset Updated(Rite rite)
        => rite.WordsErasedAtUtc.Map(erased => new DateTimeOffset(erased, TimeSpan.Zero)).IfNone(() => Midnight(rite.PublishedOnUtc));

    private static DateTimeOffset Midnight(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static string Timestamp(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // XML can't carry every character a string can (most control characters): the writer would throw, and the feed fail
    // for one stray character. They mean nothing in a rite, so they're dropped. By whole characters, so an erasure's emoji
    // (two UTF-16 halves, neither valid alone) stays.
    private static string XmlSafe(string text)
        => string.Concat(text.EnumerateRunes().Where(IsXmlChar).Select(character => character.ToString()));

    private static bool IsXmlChar(Rune character)
        => character.Value is 0x9 or 0xA or 0xD or (>= 0x20 and <= 0xD7FF) or (>= 0xE000 and <= 0xFFFD) or >= 0x10000;
}
