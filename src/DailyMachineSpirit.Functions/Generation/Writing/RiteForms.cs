using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// The forms rites take turns in (#193): left to itself, the writer titled nearly every rite "The Rite of the …", and
/// twenty of them in a row blur. Each kind has its own forms; the refill asks for the one its recent rites and waiting
/// drafts used least.
/// </summary>
public static class RiteForms
{
    public static IReadOnlyList<RiteForm> All { get; } =
    [
        new("litany", "Litany", RiteKind.Prayer, "short petitions, each answered by the same refrain"),
        new("canticle", "Canticle", RiteKind.Prayer, "praise of the Machine Spirit for something that works, and a plea that it keeps working"),
        new("lament", "Lament", RiteKind.Prayer, "grief over something lost (a deleted branch, a dead server, a night's work), and a plea"),
        new("benediction", "Benediction", RiteKind.Prayer, "a blessing spoken over a release, a deployment or a new machine as it goes out"),
        new("invocation", "Invocation", RiteKind.Prayer, "a calling on the Machine Spirit to come and act, in a moment of need"),
        new("rite", "Rite", RiteKind.Ritual, "a solemn procedure, its steps in order"),
        new("vigil", "Vigil", RiteKind.Ritual, "waiting and counting, watching for a sign that the machine has answered"),
        new("penance", "Penance", RiteKind.Ritual, "what the supplicant must do after a sin against the machine, to be forgiven"),
        new("exorcism", "Exorcism", RiteKind.Ritual, "driving something unwanted out of the machine"),
        new("catechism", "Catechism", RiteKind.Ritual, "questions a novice asks, and the answers they must recite"),
    ];

    public static Option<RiteForm> Find(string key) => All.Where(form => form.Key == key).HeadOrNone();

    /// <summary>
    /// A rite's or draft's form: the one it was asked for (its stored key), or for one written before forms took turns,
    /// the first form its title names in its first few words ("The Litany of…"), None when it names none.
    /// </summary>
    public static Option<RiteForm> Of(Option<string> asked, string title)
    {
        var stored = asked.Bind(Find);
        return stored.IsSome ? stored : Named(title);
    }

    /// <summary>
    /// The kind's form to write next: the one the recent rites and waiting drafts (their forms <paramref name="used"/>)
    /// took least, the first listed among equals.
    /// </summary>
    public static RiteForm ToWrite(RiteKind kind, IEnumerable<RiteForm> used)
    {
        var counts = used.CountBy(form => form.Key).ToDictionary();
        var forms = All.Where(form => form.Kind == kind).ToList();
        return forms.MinBy(form => counts.GetValueOrDefault(form.Key)) ?? forms[0];
    }

    private static Option<RiteForm> Named(string title)
    {
        // In the title's order: "Rite of Invocation" is a rite, whatever the list's order.
        return title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3)
            .SelectMany(word => All.Where(form => string.Equals(form.Name, word, StringComparison.OrdinalIgnoreCase)))
            .HeadOrNone();
    }
}
