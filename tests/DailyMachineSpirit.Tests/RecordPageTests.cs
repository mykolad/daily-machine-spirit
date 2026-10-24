using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using LanguageExt;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The Record's page (the Scriptorium redesign's step 2): its days, rows, filters and folded runs.</summary>
public sealed class RecordPageTests
{
    private static readonly DateTime Tuesday = new(2026, 10, 20, 20, 0, 0, DateTimeKind.Utc);

    // Newest first, a day to each heading, which says how many entries it holds; each row its time alone.
    [Fact]
    public void Entries_AreGroupedByDay_NewestFirst()
    {
        var html = Render(
            Said(AuditAction.Anoint, Tuesday.AddDays(1).AddMinutes(4), "from #2 to #1"),
            Said(AuditAction.Burn, Tuesday.AddMinutes(30), "from #3"),
            Spirit(Entry(AuditAction.Write, Tuesday)));

        Assert.True(html.IndexOf("Wednesday 21 October", StringComparison.Ordinal) < html.IndexOf("Tuesday 20 October", StringComparison.Ordinal));
        Assert.Contains("""Tuesday 20 October<span class="group-count"><span class="visually-hidden">, </span>2<span class="visually-hidden"> entries</span></span>""", html);
        Assert.Contains("""<time datetime="2026-10-21T20:04:00Z">20:04</time>""", html);
        Assert.Contains("All times UTC", html);
    }

    // The Record's own short words: a move as an arrow, a place kept as stayed, the Augury's quality, a rite's number.
    [Theory]
    [InlineData(AuditAction.Exalt, "from #2 to #1", "#2 → #1")]
    [InlineData(AuditAction.Anoint, "from #3 to #3", "stayed #3")]
    [InlineData(AuditAction.Release, "from #2 to the Augury's order", "#2 → the Augury&#39;s order")]
    [InlineData(AuditAction.JudgeAgain, "the Augury: 70 %", "Augury 70%")]
    [InlineData(AuditAction.Burn, "from #1", "from #1")]
    public void Details_ReadShort(AuditAction action, string recorded, string shown)
        => Assert.Contains($"""<span class="detail{(action == AuditAction.JudgeAgain ? " augury" : string.Empty)}">{shown}</span>""",
            Render(Said(action, Tuesday, recorded)));

    // A long detail (an old title, a precept's words) wraps within the row; a short one keeps together.
    [Fact]
    public void ALongDetail_Wraps()
    {
        var html = Render(
            Said(AuditAction.EditPrecept, Tuesday.AddMinutes(1), "was “Build rites around a familiar shortcut”"),
            Said(AuditAction.Exalt, Tuesday, "from #2 to #1"));

        Assert.Contains("""<span class="detail long">was “Build rites around a familiar shortcut”</span>""", html);
        Assert.Contains("""<span class="detail">#2 → #1</span>""", html);
    }

    [Fact]
    public void APublishedRite_ShowsItsNumber()
        => Assert.Matches("""<span class="did">published <a href="[^"]+">“Litany”</a></span> <span class="detail">No\. 7</span>""",
            Render(Spirit(Entry(AuditAction.Publish, Tuesday)) with { RiteNumber = Some(7) }));

    // Who acted colours the badge and the name; a burn has the flames' colour whoever burned.
    [Fact]
    public void Badges_SayWhoActed_AndBurnsAreDanger()
    {
        var html = Render(
            Spirit(Entry(AuditAction.Write, Tuesday.AddMinutes(2))),
            Entry(AuditAction.Anoint, Tuesday.AddMinutes(1)),
            Entry(AuditAction.Burn, Tuesday));

        Assert.Contains("""<span class="badge spirit">""", html);
        Assert.Contains("""<span class="badge scribe">""", html);
        Assert.Contains("""<span class="badge danger">""", html);
        Assert.Contains("""<span class="actor spirit">The Machine Spirit</span>""", html);
    }

    // Three or more "judged again" in a row by one actor fold into one row, each rite a click away; two don't, and
    // another entry between them breaks the run.
    [Fact]
    public void JudgedAgainInARun_FoldsIntoOneRow()
    {
        var html = Render(
            Spirit(Said(AuditAction.JudgeAgain, Tuesday.AddMinutes(5), "the Augury: 61 %")) with { Title = "Fifth" },
            Spirit(Said(AuditAction.JudgeAgain, Tuesday.AddMinutes(4), "the Augury: 72 %")) with { Title = "Fourth" },
            Spirit(Said(AuditAction.JudgeAgain, Tuesday.AddMinutes(2), "the Augury: 50 %")) with { Title = "Third" },
            Said(AuditAction.Anoint, Tuesday.AddMinutes(1), "from #1 to #1"),
            Spirit(Said(AuditAction.JudgeAgain, Tuesday.AddSeconds(30), "the Augury: 40 %")) with { Title = "Second" },
            Spirit(Said(AuditAction.JudgeAgain, Tuesday, "the Augury: 30 %")) with { Title = "First" });

        Assert.Contains("""<span class="did">judged <strong>3</strong> rites again</span> <span class="detail">20:02–20:05</span>""", html);
        Assert.Contains("""<details class="run"><summary><span class="show">Show all</span><span class="hide">Hide</span>""", html);
        Assert.Contains("""“Fourth”</a><time datetime="2026-10-20T20:04:00Z">20:04</time><span class="augury">72%</span>""", html);
        Assert.Contains("""<span class="did">judged <a href=""", html);
        Assert.Equal(2, html.Split("<details class=\"run\">").Length);
    }

    // The counts follow who's chosen, and a kind none of their entries is goes unoffered; the links keep both choices.
    [Fact]
    public void Filters_NarrowByWhoAndWhat()
    {
        IReadOnlyList<AuditEntry> entries =
        [
            Spirit(Entry(AuditAction.Publish, Tuesday.AddMinutes(3))),
            Spirit(Entry(AuditAction.Write, Tuesday.AddMinutes(2))),
            Said(AuditAction.Exalt, Tuesday.AddMinutes(1), "from #2 to #1"),
            Said(AuditAction.Burn, Tuesday, "from #3"),
        ];

        var scribesMoving = Render(entries, new RecordFilter(RecordActors.Scribes, Some(RecordKind.Moved), None));

        Assert.Contains("""<a class="seg" href="/scriptorium/record?what=moved">Everyone<span class="chip-count">4</span></a>""", scribesMoving);
        Assert.Contains("""<a class="seg" href="/scriptorium/record?who=scribes&amp;what=moved" aria-current="true">Scribes<span class="chip-count">2</span></a>""", scribesMoving);
        Assert.Contains("""<a class="chip" href="/scriptorium/record?who=scribes&amp;what=moved" aria-current="true">Moved<span class="chip-count">1</span></a>""", scribesMoving);
        Assert.DoesNotContain(">Published<", scribesMoving);
        Assert.Contains("exalted", scribesMoving);
        Assert.DoesNotContain("consigned", scribesMoving);
        Assert.Contains("Nothing in the Record matches.", Render(entries, new RecordFilter(RecordActors.MachineSpirit, Some(RecordKind.Burned), None)));
    }

    // On an older page, the filters narrow its days, as their counts do, rather than jumping back to the newest.
    [Fact]
    public void Filters_OnAnOlderPage_KeepItsDay()
    {
        var olderPage = Render([Said(AuditAction.Exalt, Tuesday, "from #2 to #1")], new RecordFilter(RecordActors.Everyone, None, Some(new DateOnly(2026, 10, 20))));

        Assert.Contains("""<a class="seg" href="/scriptorium/record?who=scribes&amp;from=2026-10-20">Scribes""", olderPage);
        Assert.Contains("""<a class="chip" href="/scriptorium/record?what=moved&amp;from=2026-10-20">Moved""", olderPage);
    }

    [Fact]
    public void OlderDays_LeadOn_KeepingTheFilters()
        => Assert.Contains("""<a href="/scriptorium/record?who=spirit&amp;from=2026-10-19">Older days →</a>""",
            ScriptoriumPage.RenderRecord(new RecordShown(
                [Spirit(Entry(AuditAction.Write, Tuesday))], [], None, Option<NameInTheRecord>.None, None, None,
                new RecordFilter(RecordActors.MachineSpirit, None, None), Some(new DateOnly(2026, 10, 19)))));

    // As many read as could be: the oldest day may go on past them, so it's left for the next page, never split.
    [Fact]
    public void WholeDays_LeaveTheOldestDay_WhenItMayGoOn()
    {
        IReadOnlyList<AuditEntry> read = [Entry(AuditAction.Write, Tuesday.AddDays(1)), Entry(AuditAction.Write, Tuesday.AddHours(1)), Entry(AuditAction.Write, Tuesday)];

        var (whole, olderFrom) = RecordShown.WholeDays(read, 3);
        var (all, nothingOlder) = RecordShown.WholeDays(read, 4);
        var (oneDay, dayBefore) = RecordShown.WholeDays([.. read.Skip(1)], 2);

        Assert.Equal([read[0]], whole);
        Assert.Equal(Some(DateOnly.FromDateTime(Tuesday)), olderFrom);
        Assert.Equal(read, all);
        Assert.Equal(None, nothingOlder);
        Assert.Equal(2, oneDay.Count);
        Assert.Equal(Some(DateOnly.FromDateTime(Tuesday).AddDays(-1)), dayBefore);
    }

    private static string Render(params AuditEntry[] entries) => RecordPages.Of(entries, []);

    private static string Render(IReadOnlyList<AuditEntry> entries, RecordFilter filter)
        => ScriptoriumPage.RenderRecord(new RecordShown(entries, [], None, Option<NameInTheRecord>.None, None, None, filter, None));

    // A Scribe's entry about a rite of its own, with no detail.
    private static AuditEntry Entry(AuditAction action, DateTime atUtc)
        => new() { Id = Guid.NewGuid(), Actor = Tester, Action = action, Title = "Litany", DraftId = Some(Guid.NewGuid()), AtUtc = atUtc };

    private static AuditEntry Said(AuditAction action, DateTime atUtc, string detail) => Entry(action, atUtc) with { Detail = Some(detail) };

    private static AuditEntry Spirit(AuditEntry entry) => entry with { Actor = Actor.MachineSpirit };
}
