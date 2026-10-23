using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// The Record (the Scriptorium redesign's step 2, <c>design_handoff_scriptorium_calendar</c>): who changed what, newest
/// first, by day, narrowed to whose entries and what kind; or one draft's record, from its writing to its publishing.
/// </summary>
public static partial class ScriptoriumPage
{
    // So many "judged again" in a row, by one actor, fold into one row: a refill judges every draft it couldn't before.
    private const int JudgedInARun = 3;

    private const string AriaCurrent = " aria-current=\"true\"";

    private static readonly (RecordKind Kind, string Key, string Label)[] Kinds =
    [
        (RecordKind.Published, "published", "Published"),
        (RecordKind.Written, "written", "Written"),
        (RecordKind.Judged, "judged", "Judged"),
        (RecordKind.Moved, "moved", "Moved"),
        (RecordKind.Burned, "burned", "Burned & restored"),
        (RecordKind.Other, "other", "Other"),
    ];

    public static string RenderRecord(RecordShown shown)
    {
        var html = new StringBuilder();
        Head(html, "The Record", TabBar(ScriptoriumTab.Record, shown.Counts), Identity(shown.Name));
        html.Append("""<main id="main">""");
        Heading(html, "The Record",
            "Every change to the calendar, the rites and the Precepts, and who made it, newest first: the Scribes' actions and the Machine Spirit's own.");
        shown.Draft.IfSome(_ => html.Append($"""<p>One rite's record.{shown.Found.Bind(rite => WhereItIs(rite, shown.Entries)).Map(link => $" {link} ·").IfNone(string.Empty)} <a href="/scriptorium/record">The whole record</a></p>"""));
        if (shown.Entries.Count == 0)
        {
            html.Append("""<p class="empty record-empty">Nothing is recorded yet.</p>""");
        }
        else
        {
            var byWho = shown.Entries.Where(entry => shown.Filter.Who switch
            {
                RecordActors.Scribes => entry.Actor != AuditEntry.MachineSpirit,
                RecordActors.MachineSpirit => entry.Actor == AuditEntry.MachineSpirit,
                _ => true,
            }).ToList();
            var matching = byWho.Where(entry => shown.Filter.What.ForAll(kind => KindOf(entry.Action) == kind)).ToList();
            if (shown.Draft.IsSome)
            {
                html.Append("""<p class="utc">All times UTC</p>""");
            }
            else
            {
                RecordFilters(html, shown, byWho);
            }
            if (matching.Count == 0)
            {
                html.Append("""<p class="empty record-empty">Nothing in the Record matches.</p>""");
            }
            foreach (var day in matching.GroupBy(entry => DateOnly.FromDateTime(entry.AtUtc)))
            {
                RecordDay(html, day.Key, [.. day], shown.Names);
            }
            shown.OlderFrom.IfSome(day =>
                html.Append($"""<p class="older"><a href="{RecordAddress(shown.Filter, Some(day))}">Older days →</a></p>"""));
        }
        html.Append("</main>");
        Footer(html, None);
        return html.ToString();
    }

    /// <summary>The kind a filter's address names (<c>?what=moved</c>); none for any other word.</summary>
    public static Option<RecordKind> RecordKindNamed(string key)
        => Kinds.Where(kind => kind.Key == key).Select(kind => kind.Kind).HeadOrNone();

    // Who, as one choice of three, and what, as chips counting what the who leaves (none for a kind it leaves none of).
    // Links, so the filters work without a script, and an address keeps them.
    private static void RecordFilters(StringBuilder html, RecordShown shown, IReadOnlyList<AuditEntry> byWho)
    {
        var spirit = shown.Entries.Count(entry => entry.Actor == AuditEntry.MachineSpirit);
        html.Append("""<div class="record-filters"><div class="who" role="group" aria-label="Who">""");
        foreach (var (who, label, count) in new[]
        {
            (RecordActors.Everyone, "Everyone", shown.Entries.Count),
            (RecordActors.Scribes, "Scribes", shown.Entries.Count - spirit),
            (RecordActors.MachineSpirit, "The Machine Spirit", spirit),
        })
        {
            var current = shown.Filter.Who == who ? AriaCurrent : string.Empty;
            html.Append($"""<a class="seg" href="{RecordAddress(shown.Filter with { Who = who }, None)}"{current}>{label}<span class="chip-count">{count}</span></a>""");
        }
        html.Append("""</div><div class="what-row"><div class="subjects" role="group" aria-label="What">""");
        var all = shown.Filter.What.IsNone ? AriaCurrent : string.Empty;
        html.Append($"""<a class="chip" href="{RecordAddress(shown.Filter with { What = None }, None)}"{all}>All<span class="chip-count">{byWho.Count}</span></a>""");
        foreach (var (kind, _, label) in Kinds)
        {
            var count = byWho.Count(entry => KindOf(entry.Action) == kind);
            if (count == 0)
            {
                continue;
            }
            var current = shown.Filter.What == Some(kind) ? AriaCurrent : string.Empty;
            html.Append($"""<a class="chip" href="{RecordAddress(shown.Filter with { What = Some(kind) }, None)}"{current}>{Encode(label)}<span class="chip-count">{count}</span></a>""");
        }
        html.Append("""</div><p class="utc">All times UTC</p></div></div>""");
    }

    private static void RecordDay(StringBuilder html, DateOnly day, IReadOnlyList<AuditEntry> entries, IReadOnlyList<ScribeName> names)
    {
        var id = $"day-{day:yyyy-MM-dd}";
        html.Append($"""<section class="record-day" aria-labelledby="{id}"><h2 id="{id}">{day.ToString("dddd d MMMM", CultureInfo.InvariantCulture)}<span class="group-count"><span class="visually-hidden">, </span>{entries.Count}<span class="visually-hidden"> {(entries.Count == 1 ? "entry" : "entries")}</span></span></h2><ol class="record" role="list">""");
        for (var at = 0; at < entries.Count;)
        {
            var run = entries.Skip(at)
                .TakeWhile(entry => entry.Action == AuditAction.JudgeAgain && entry.Actor == entries[at].Actor)
                .ToList();
            if (run.Count >= JudgedInARun)
            {
                JudgedRun(html, run, names);
                at += run.Count;
            }
            else
            {
                RecordEntry(html, entries[at], names);
                at++;
            }
        }
        html.Append("</ol></section>");
    }

    private static void RecordEntry(StringBuilder html, AuditEntry entry, IReadOnlyList<ScribeName> names)
    {
        var detail = Chip(entry).Map(chip => $""" <span class="detail{(entry.Action == AuditAction.JudgeAgain ? " augury" : string.Empty)}">{Encode(chip)}</span>""")
            .IfNone(string.Empty);
        html.Append($"""<li>{HoursAndMinutes(entry.AtUtc)}{Badge(entry)}<p class="said">{Actor(entry, names)} <span class="did">{Did(entry)}</span>{detail}</p></li>""");
    }

    // The run's newest time on the row, its span in the chip, and each rite in the fold: its title, time and quality.
    private static void JudgedRun(StringBuilder html, IReadOnlyList<AuditEntry> run, IReadOnlyList<ScribeName> names)
    {
        var span = $"{Clock(run[^1].AtUtc)}–{Clock(run[0].AtUtc)}";
        html.Append($"""<li>{HoursAndMinutes(run[0].AtUtc)}{Badge(run[0])}<div class="said">{Actor(run[0], names)} <span class="did">judged <strong>{run.Count}</strong> rites again</span> <span class="detail">{span}</span>""");
        html.Append($"""<details class="run"><summary><span class="show">Show all</span><span class="hide">Hide</span>{ScriptoriumIcons.Chevron}</summary><ul class="run-list" role="list">""");
        foreach (var entry in run)
        {
            var quality = Chip(entry).Map(chip => chip.Replace("Augury ", string.Empty, StringComparison.Ordinal)).IfNone(string.Empty);
            html.Append($"""<li>{RiteLink(entry)}{HoursAndMinutes(entry.AtUtc)}<span class="augury">{Encode(quality)}</span></li>""");
        }
        html.Append("</ul></details></div></li>");
    }

    private static string HoursAndMinutes(DateTime atUtc)
        => $"""<time datetime="{atUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}">{Clock(atUtc)}</time>""";

    private static string Clock(DateTime atUtc) => atUtc.ToString("HH:mm", CultureInfo.InvariantCulture);

    // Coloured by who acted, the Machine Spirit or a Scribe; a burn in the flames' own.
    private static string Badge(AuditEntry entry)
    {
        var tone = entry.Action == AuditAction.Burn ? "danger" : entry.Actor == AuditEntry.MachineSpirit ? "spirit" : "scribe";
        var icon = entry.Action switch
        {
            AuditAction.Write or AuditAction.Amend or AuditAction.EditPrecept => ScriptoriumIcons.Pencil,
            AuditAction.Publish => ScriptoriumIcons.BookOpen,
            AuditAction.JudgeAgain or AuditAction.LetTheAuguryDecide => ScriptoriumIcons.Augury,
            AuditAction.Anoint => ScriptoriumIcons.Anoint,
            AuditAction.Exalt => ScriptoriumIcons.Exalt,
            AuditAction.Humble => ScriptoriumIcons.Humble,
            AuditAction.Release => ScriptoriumIcons.Release,
            AuditAction.Burn => ScriptoriumIcons.Burn,
            AuditAction.Restore => ScriptoriumIcons.Restore,
            AuditAction.Summon => ScriptoriumIcons.Summon,
            AuditAction.ChooseName => ScriptoriumIcons.User,
            AuditAction.Distil => ScriptoriumIcons.Flask,
            AuditAction.AddPrecept => ScriptoriumIcons.Add,
            AuditAction.PinPrecept => ScriptoriumIcons.PinFilled,
            AuditAction.UnpinPrecept => ScriptoriumIcons.Pin,
            AuditAction.DeletePrecept => ScriptoriumIcons.Delete,
            _ => ScriptoriumIcons.Eraser,
        };
        return $"""<span class="badge {tone}">{icon}</span>""";
    }

    private static string Actor(AuditEntry entry, IReadOnlyList<ScribeName> names)
        => $"""<span class="actor {(entry.Actor == AuditEntry.MachineSpirit ? "spirit" : "scribe")}">{Encode(ScribeNames.Of(entry.Actor, names))}</span>""";

    // What an entry records, after who: the rite (or precept) named by its words then, a rite linking to its own record.
    private static string Did(AuditEntry entry)
    {
        var rite = RiteLink(entry);
        return entry.Action switch
        {
            AuditAction.Anoint => $"anointed {rite}",
            AuditAction.Exalt => $"exalted {rite}",
            AuditAction.Humble => $"humbled {rite}",
            AuditAction.Burn => $"consigned {rite} to the flames",
            AuditAction.Restore => $"restored {rite} from the ashes",
            AuditAction.Amend => $"amended {rite}",
            AuditAction.Summon => "summoned new rites",
            AuditAction.LetTheAuguryDecide => "let the Augury decide",
            AuditAction.Release => $"released {rite} to the Augury",
            AuditAction.Write => $"wrote {rite}",
            AuditAction.Distil => """distilled <a href="/scriptorium/precepts">the Precepts</a>""",
            AuditAction.AddPrecept => $"added the precept {rite}",
            AuditAction.EditPrecept => $"edited the precept {rite}",
            AuditAction.PinPrecept => $"pinned the precept {rite}",
            AuditAction.UnpinPrecept => $"unpinned the precept {rite}",
            AuditAction.DeletePrecept => $"deleted the precept {rite}",
            AuditAction.JudgeAgain => $"judged {rite} again",
            AuditAction.ChooseName => $"chose the name {rite}",
            AuditAction.Expurgate => $"erased words from {rite}",
            _ => $"published {rite}",
        };
    }

    private static string RiteLink(AuditEntry entry)
        => entry.DraftId.Match(
            Some: id => $"""<a href="/scriptorium/record?draft={id:D}">“{WithCode(entry.Title)}”</a>""",
            None: () => $"“{WithCode(entry.Title)}”");

    // The entry's detail in the Record's own short words: a move as "#2 → #1", the Augury's quality as a percentage,
    // a published or erased rite's number. Anything else as it was recorded.
    private static Option<string> Chip(AuditEntry entry)
    {
        if (entry.Action is AuditAction.Publish or AuditAction.Expurgate && entry.RiteNumber.IsSome)
        {
            var number = entry.RiteNumber.Map(found => $"No. {found}");
            return entry.Detail.Match(Some: detail => number.Map(found => $"{found} · {detail}"), None: () => number);
        }
        return entry.Detail.Map(detail =>
        {
            var move = Move().Match(detail);
            if (move.Success)
            {
                return move.Groups["to"].Value == move.Groups["from"].Value ? $"stayed #{move.Groups["from"].Value}" : $"#{move.Groups["from"].Value} → #{move.Groups["to"].Value}";
            }
            var released = Released().Match(detail);
            if (released.Success)
            {
                return $"#{released.Groups["from"].Value} → {released.Groups["to"].Value}";
            }
            var quality = Quality().Match(detail);
            return quality.Success ? $"Augury {quality.Groups["percent"].Value}%" : detail;
        });
    }

    private static RecordKind KindOf(AuditAction action) => action switch
    {
        AuditAction.Publish => RecordKind.Published,
        AuditAction.Write or AuditAction.Amend => RecordKind.Written,
        AuditAction.JudgeAgain => RecordKind.Judged,
        AuditAction.Anoint or AuditAction.Exalt or AuditAction.Humble or AuditAction.Release => RecordKind.Moved,
        AuditAction.Burn or AuditAction.Restore => RecordKind.Burned,
        _ => RecordKind.Other,
    };

    private static string RecordAddress(RecordFilter filter, Option<DateOnly> from)
    {
        var query = new List<string>();
        if (filter.Who != RecordActors.Everyone)
        {
            query.Add($"who={(filter.Who == RecordActors.Scribes ? "scribes" : "spirit")}");
        }
        filter.What.IfSome(kind => query.Add($"what={Kinds.First(named => named.Kind == kind).Key}"));
        from.IfSome(day => query.Add($"from={day:yyyy-MM-dd}"));
        return "/scriptorium/record" + (query.Count == 0 ? string.Empty : "?" + string.Join("&amp;", query));
    }

    // Where one rite's record leads to the rite itself: its card in the calendar or the Ashes, opened and marked, or its
    // page once published (its number is in the publish entry). None for a draft published before the Record kept numbers.
    private static Option<string> WhereItIs(Draft draft, IReadOnlyList<AuditEntry> entries)
    {
        var id = draft.Id.ToString("D");
        return draft.State switch
        {
            DraftState.Waiting => Some($"""<a href="/scriptorium?rite={id}#rite-{id}">See it in the calendar</a>"""),
            DraftState.Burned => Some($"""<a href="/scriptorium/ashes?rite={id}#rite-{id}">See it in the Ashes</a>"""),
            _ => entries.Where(entry => entry.Action == AuditAction.Publish).Bind(entry => entry.RiteNumber.ToSeq()).HeadOrNone()
                .Map(number => $"""<a href="/r/{number}">Read it, NO. {number}</a>"""),
        };
    }

    [GeneratedRegex(@"^from #(?<from>\d+) to #(?<to>\d+)$")]
    private static partial Regex Move();

    [GeneratedRegex(@"^from #(?<from>\d+) to (?<to>the Augury's order)$")]
    private static partial Regex Released();

    [GeneratedRegex(@"^the Augury: (?<percent>\d+)\s?%$")]
    private static partial Regex Quality();
}
