using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Options;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// A draft in the Liturgical Calendar, as the Scriptorium shows it. <see cref="PlacedBy"/> is the Scribe who placed it
/// (their Access id), when the order knows.
/// </summary>
public sealed record CalendarEntry(Draft Draft, DateOnly PublishesOnUtc, bool PlacedByScribes, Option<string> PlacedBy, int AuguryPlace);

/// <summary>
/// <see cref="NowUtc"/> tells whether the first entry goes out at the coming midnight, or is already due, and how long
/// until it does.
/// </summary>
public sealed record ScriptoriumView(List<CalendarEntry> Calendar, List<ScribeDecision> Decisions, DateTime NowUtc)
{
    public DateOnly Today => DateOnly.FromDateTime(NowUtc);
}

/// <summary>
/// What the Scribes of the Scriptorium do: anoint, exalt and humble drafts (placing them in their order, ahead of the
/// Augury's), consign them to the flames and restore them. Each records a <see cref="ScribeDecision"/>, with the
/// Scribe's note if they left one, for the Augury to learn from (#12).
/// </summary>
public sealed class Scribes
{
    public static readonly Error NoteTooLong = Error.New($"A note can be at most {ScribeDecision.MaxNoteLength} characters.");

    /// <summary>Exalting the first draft or humbling the last: a page shown before another Scribe moved it.</summary>
    public static readonly Error NothingToMove = Error.New("The draft is already at that end of the calendar.");

    private readonly IDraftRepository drafts;
    private readonly IRiteRepository rites;
    private readonly IScriptoriumRepository scriptorium;
    private readonly IOptions<AuguryOptions> augury;
    private readonly IOptions<ScriptoriumPagesOptions> pages;
    private readonly TimeProvider time;

    public Scribes(
        IDraftRepository drafts,
        IRiteRepository rites,
        IScriptoriumRepository scriptorium,
        IOptions<AuguryOptions> augury,
        IOptions<ScriptoriumPagesOptions> pages,
        TimeProvider time)
    {
        this.drafts = drafts;
        this.rites = rites;
        this.scriptorium = scriptorium;
        this.augury = augury;
        this.pages = pages;
        this.time = time;
    }

    public Task<Either<Error, ScriptoriumView>> View(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        return Calendar(cancellationToken)
            .BindAsync(calendar => rites.GetPublishedOn(today, cancellationToken)
                .BindAsync(todays => scriptorium.GetDecisions(pages.Value.JudgmentsShown, cancellationToken)
                    .MapAsync(decisions =>
                    {
                        // The next one goes out at the coming midnight: tonight's, unless today's is still to come.
                        var first = todays.IsSome ? today.AddDays(1) : today;
                        var placed = calendar.Placements.Order.ToHashSet();
                        var entries = calendar.Ordered
                            .Select((draft, place) => new CalendarEntry(
                                draft, first.AddDays(place), placed.Contains(draft.Id), Placer(calendar.Placements, draft.Id),
                                calendar.AuguryPlace(draft)))
                            .ToList();
                        return Task.FromResult(new ScriptoriumView(entries, decisions, now));
                    })));
    }

    /// <summary>The Ashes: the drafts burned most recently, the newest first.</summary>
    public Task<Either<Error, List<Draft>>> Ashes(CancellationToken cancellationToken)
        => scriptorium.GetBurned(pages.Value.AshesShown, cancellationToken);

    /// <summary>The draft, if it's burned: what the calendar's message names after a burn, the draft having left it.</summary>
    public Task<Either<Error, Option<Draft>>> FindBurned(Guid draftId, CancellationToken cancellationToken)
        => drafts.Get(draftId, cancellationToken)
            .MapAsync(found => Task.FromResult(found.Filter(draft => draft.State == DraftState.Burned)));

    /// <summary>Makes the draft the next to be published; for the first, it keeps it there, ahead of the Augury's later picks.</summary>
    public Task<Either<Error, Unit>> Anoint(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Anoint, note, scribe, (order, index) => Some<List<Guid>>([order[index], .. order[..index]]), cancellationToken);

    /// <summary>Moves the draft up one place; everything above it is now in the Scribes' order.</summary>
    public Task<Either<Error, Unit>> Exalt(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Exalt, note, scribe, (order, index) => index == 0
            ? None
            : Some<List<Guid>>([.. order[..(index - 1)], order[index], order[index - 1]]), cancellationToken);

    /// <summary>Moves the draft down one place; the one it swaps with is now in the Scribes' order too.</summary>
    public Task<Either<Error, Unit>> Humble(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Humble, note, scribe, (order, index) => index == order.Count - 1
            ? None
            : Some<List<Guid>>([.. order[..index], order[index + 1], order[index]]), cancellationToken);

    /// <summary>
    /// Burns the draft (it's kept, hidden, and can be restored) and takes it out of the Scribes' order; returns how many
    /// drafts are waiting after it. Counted after the burn is saved, so when two Scribes burn the last two at once, the
    /// later one sees none left. None when that count fails: the burn is saved all the same, and the caller can't tell
    /// whether any draft is left.
    /// </summary>
    public Task<Either<Error, Option<int>>> Burn(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => Calendar(cancellationToken)
            .BindAsync(calendar => calendar.Find(draftId).Match(
                Some: found => scriptorium
                    .ChangeState(draftId, DraftState.Waiting, DraftState.Burned,
                        Decision(found.Draft, ScribeAction.Burn, checkedNote, Some(calendar.PlacesOf(found))),
                        WithoutDraft(calendar.Placements, draftId),
                        AuditEntries.Of(time, scribe, AuditAction.Burn, found.Draft, $"from #{found.Place}"),
                        cancellationToken)
                    .MapAsync(async _ => (await drafts.GetWaiting(cancellationToken)).Match(
                        Right: waiting => Some(waiting.Count),
                        Left: _ => Option<int>.None)),
                None: () => Task.FromResult(Left<Error, Option<int>>(ScriptoriumRepository.ChangedMeanwhile)))));

    /// <summary>
    /// Restores a burned draft, however long ago it burned: it waits again, in the Augury's order. Given the place it had
    /// in the Scribes' order (1 is next), as the calendar's Undo gives it straight after a burn, it goes back there.
    /// </summary>
    public Task<Either<Error, Unit>> Restore(Guid draftId, string note, string scribe, Option<int> scribesPlace, CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => drafts.Get(draftId, cancellationToken)
            .BindAsync(stored => Restored(draftId, scribesPlace, scribe, cancellationToken)
                .BindAsync(restored => stored.Filter(draft => draft.State == DraftState.Burned).Match(
                    Some: draft => scriptorium
                        .ChangeState(draftId, DraftState.Burned, DraftState.Waiting,
                            Decision(draft, ScribeAction.Restore, checkedNote, None),
                            restored.Placements,
                            AuditEntries.Of(time, scribe, AuditAction.Restore, draft, restored.Place.Map(place => $"to #{place}")),
                            cancellationToken)
                        .MapAsync(_ => Task.FromResult(unit)),
                    None: () => Task.FromResult(Left<Error, Unit>(ScriptoriumRepository.ChangedMeanwhile))))));

    /// <summary>
    /// Where a waiting draft is in the calendar (1 is next), and where the Augury alone would put it: what an amendment's
    /// decision keeps, as every other decision does. None when it isn't waiting.
    /// </summary>
    public Task<Either<Error, Option<DraftPlaces>>> PlacesOf(Guid draftId, CancellationToken cancellationToken)
        => Calendar(cancellationToken).MapAsync(calendar => Task.FromResult(calendar.Find(draftId).Map(calendar.PlacesOf)));

    /// <summary>Forgets the Scribes' order, so the Augury orders every draft.</summary>
    public Task<Either<Error, Unit>> LetTheAuguryDecide(string scribe, CancellationToken cancellationToken)
        => scriptorium.GetPlacements(cancellationToken)
            .BindAsync(placements => scriptorium.SavePlacements(placements with { Order = [] }, None, [],
                AuditEntries.Of(time, scribe, AuditAction.LetTheAuguryDecide, None, None), cancellationToken));

    private Task<Either<Error, Unit>> Reorder(
        Guid draftId, ScribeAction action, string note, string scribe, Func<List<Guid>, int, Option<List<Guid>>> reorder,
        CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => Calendar(cancellationToken)
            .BindAsync(calendar => calendar.Find(draftId).Match(
                Some: found => reorder(calendar.Ordered.Select(draft => draft.Id).ToList(), found.Place - 1).Match(
                    // Every draft in the moved stretch must still wait: it was read waiting, and the new order places it.
                    Some: moved => scriptorium.SavePlacements(
                        Placed(calendar.Placements, KeepingTheRest(moved, calendar), draftId, scribe),
                        Decision(found.Draft, action, checkedNote, Some(calendar.PlacesOf(found))),
                        moved,
                        AuditEntries.Of(time, scribe, Audited(action), found.Draft, $"from #{found.Place} to #{moved.IndexOf(draftId) + 1}"),
                        cancellationToken),
                    // Already at the top (or bottom): nothing changes, so there's nothing to learn, and the page says so.
                    None: () => Task.FromResult(Left<Error, Unit>(NothingToMove))),
                None: () => Task.FromResult(Left<Error, Unit>(ScriptoriumRepository.ChangedMeanwhile)))));

    // The reordered top of the calendar, then the drafts the Scribes placed below it, still in their order: moving one
    // draft never undoes an earlier decision about another.
    private static List<Guid> KeepingTheRest(List<Guid> moved, CalendarState calendar)
    {
        var placedBelow = calendar.Ordered.Select(draft => draft.Id)
            .Where(id => calendar.Placements.Order.Contains(id) && !moved.Contains(id));
        return [.. moved, .. placedBelow];
    }

    // The new order, and who placed each draft in it: the Scribe who moved it, or who placed it by moving another. One
    // already placed keeps the Scribe who placed it, unless it's the one moved.
    private static Placements Placed(Placements before, List<Guid> order, Guid moved, string scribe)
        => before with
        {
            Order = order,
            PlacedBy = order
                .Select(id => (Id: id, Placer: id == moved || !before.Order.Contains(id) ? Some(scribe) : Placer(before, id)))
                .SelectMany(placed => placed.Placer.Map(placer => KeyValuePair.Create(placed.Id, placer)))
                .ToDictionary(),
        };

    private static Option<string> Placer(Placements placements, Guid draftId)
        => placements.PlacedBy.TryGetValue(draftId, out var placer) ? Some(placer) : None;

    // The Scribes' order once the draft is restored, if it changes, and the draft's place in it. Without a place to go
    // back to, the draft starts again in the Augury's order; the calendar is read only to find the place.
    private Task<Either<Error, (Option<Placements> Placements, Option<int> Place)>> Restored(
        Guid draftId, Option<int> scribesPlace, string scribe, CancellationToken cancellationToken)
        => scribesPlace.Match(
            Some: place => Calendar(cancellationToken).MapAsync(calendar =>
            {
                var (placements, placed) = BackInPlace(calendar, draftId, place, scribe);
                return Task.FromResult((Some(placements), Some(placed)));
            }),
            None: () => scriptorium.GetPlacements(cancellationToken)
                .MapAsync(placements => Task.FromResult((WithoutDraft(placements, draftId), Option<int>.None))));

    // Back in its place among the drafts the Scribes placed, or last when fewer are placed now, placed by the restoring
    // Scribe: the order forgets who placed a draft when it leaves, and putting it back is placing it.
    private static (Placements Placements, int Place) BackInPlace(CalendarState calendar, Guid draftId, int place, string scribe)
    {
        var placed = calendar.Ordered.Select(draft => draft.Id).Where(calendar.Placements.Order.Contains).ToList();
        var index = Math.Clamp(place - 1, 0, placed.Count);
        var placedBy = calendar.Placements.PlacedBy.ToDictionary();
        placedBy[draftId] = scribe;
        return (calendar.Placements with { Order = [.. placed[..index], draftId, .. placed[index..]], PlacedBy = placedBy }, index + 1);
    }

    // A burned or restored draft leaves the Scribes' order: a restored one starts again in the Augury's.
    private static Option<Placements> WithoutDraft(Placements placements, Guid draftId)
        => placements.Order.Contains(draftId)
            ? Some(placements with { Order = placements.Order.Where(id => id != draftId).ToList() })
            : None;

    // The Scribes' order is read first: a draft placed after that changes the order's ETag, so saving a change based on a
    // waiting list without that draft fails (ChangedMeanwhile) rather than dropping its placement.
    private Task<Either<Error, CalendarState>> Calendar(CancellationToken cancellationToken)
        => scriptorium.GetPlacements(cancellationToken)
            .BindAsync(placements => drafts.GetWaiting(cancellationToken)
                .BindAsync(waiting => rites.GetNewest(augury.Value.RecentRitesCompared, cancellationToken)
                    .MapAsync(recent => Task.FromResult(new CalendarState(
                        LiturgicalCalendar.Order(waiting, recent, placements.Order, augury.Value),
                        LiturgicalCalendar.Order(waiting, recent, [], augury.Value),
                        placements)))));

    private ScribeDecision Decision(Draft draft, ScribeAction action, Option<string> note, Option<DraftPlaces> places)
        => new()
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Action = action,
            Kind = draft.Writing.Kind,
            Title = draft.Writing.Title,
            Text = draft.Writing.Text,
            HereticalTruth = draft.Writing.HereticalTruth,
            CalendarPlace = places.Map(found => found.CalendarPlace),
            AuguryPlace = places.Map(found => found.AuguryPlace),
            AuguryRankedByQuality = places.Exists(found => found.AuguryRankedByQuality),
            AuguryQuality = draft.Augury.Map(augury => augury.Quality),
            Note = note,
            DecidedAtUtc = time.GetUtcNow().UtcDateTime,
        };

    private static AuditAction Audited(ScribeAction action) => action switch
    {
        ScribeAction.Anoint => AuditAction.Anoint,
        ScribeAction.Exalt => AuditAction.Exalt,
        _ => AuditAction.Humble,
    };

    // An empty note is no note.
    private static Either<Error, Option<string>> Note(string note)
    {
        var trimmed = note.Trim();
        if (trimmed.Length > ScribeDecision.MaxNoteLength)
        {
            return NoteTooLong;
        }
        return trimmed.Length == 0 ? Option<string>.None : Some(trimmed);
    }

    /// <summary>The calendar as it is now, and the Augury's order alone, to record how far the Scribes disagree with it.</summary>
    private sealed record CalendarState(List<Draft> Ordered, List<Draft> AuguryOrder, Placements Placements)
    {
        /// <summary>The draft and its place (1 is next), if it's waiting.</summary>
        public Option<(Draft Draft, int Place)> Find(Guid draftId)
        {
            var index = Ordered.FindIndex(draft => draft.Id == draftId);
            return index < 0 ? None : Some((Ordered[index], index + 1));
        }

        public int AuguryPlace(Draft draft) => AuguryOrder.FindIndex(other => other.Id == draft.Id) + 1;

        public DraftPlaces PlacesOf((Draft Draft, int Place) found)
            => new(found.Place, AuguryPlace(found.Draft), LiturgicalCalendar.RanksByQuality(found.Draft, AuguryOrder));
    }
}
