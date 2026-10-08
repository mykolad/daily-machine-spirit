using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium;

public class ScriptoriumOptions
{
    public const string SectionName = "Scriptorium";

    /// <summary>
    /// Off unless set: on staging, which only its owner's address can reach. Production turns it on once the Scribes sign
    /// in with Cloudflare Access.
    /// </summary>
    public bool Enabled { get; set; }
}

/// <summary>A draft in the Liturgical Calendar, as the Scriptorium shows it.</summary>
public sealed record CalendarEntry(Draft Draft, DateOnly PublishesOnUtc, bool PlacedByScribes, int AuguryPlace);

public sealed record ScriptoriumView(List<CalendarEntry> Calendar, List<Draft> Ashes, List<ScribeDecision> Decisions);

/// <summary>
/// What the Scribes of the Scriptorium do: anoint, exalt and humble drafts (placing them in their order, ahead of the
/// Augury's), consign them to the flames and restore them. Each records a <see cref="ScribeDecision"/>, with the
/// Scribe's note if they left one, for the Augury to learn from (#12).
/// </summary>
public sealed class Scribes
{
    /// <summary>How many burned drafts and decisions the Scriptorium shows.</summary>
    public const int Shown = 20;

    public static readonly Error NoteTooLong = Error.New($"A note can be at most {ScribeDecision.MaxNoteLength} characters.");

    private readonly IDraftRepository drafts;
    private readonly IRiteRepository rites;
    private readonly IScriptoriumRepository scriptorium;
    private readonly TimeProvider time;

    public Scribes(IDraftRepository drafts, IRiteRepository rites, IScriptoriumRepository scriptorium, TimeProvider time)
    {
        this.drafts = drafts;
        this.rites = rites;
        this.scriptorium = scriptorium;
        this.time = time;
    }

    public Task<Either<Error, ScriptoriumView>> View(CancellationToken cancellationToken)
    {
        var today = Today;
        return Calendar(cancellationToken)
            .BindAsync(calendar => rites.GetPublishedOn(today, cancellationToken)
                .BindAsync(todays => scriptorium.GetBurned(Shown, cancellationToken)
                    .BindAsync(ashes => scriptorium.GetDecisions(Shown, cancellationToken)
                        .MapAsync(decisions =>
                        {
                            // The next one goes out at the coming midnight: tonight's, unless today's is still to come.
                            var first = todays.IsSome ? today.AddDays(1) : today;
                            var placed = calendar.Placements.Order.ToHashSet();
                            var entries = calendar.Ordered
                                .Select((draft, place) => new CalendarEntry(
                                    draft, first.AddDays(place), placed.Contains(draft.Id), calendar.AuguryPlace(draft)))
                                .ToList();
                            return Task.FromResult(new ScriptoriumView(entries, ashes, decisions));
                        }))));
    }

    /// <summary>Makes the draft the next to be published; for the first, it keeps it there, ahead of the Augury's later picks.</summary>
    public Task<Either<Error, Unit>> Anoint(Guid draftId, string note, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Anoint, note, (order, index) => Some<List<Guid>>([order[index], .. order[..index]]), cancellationToken);

    /// <summary>Moves the draft up one place; everything above it is now in the Scribes' order.</summary>
    public Task<Either<Error, Unit>> Exalt(Guid draftId, string note, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Exalt, note, (order, index) => index == 0
            ? None
            : Some<List<Guid>>([.. order[..(index - 1)], order[index], order[index - 1]]), cancellationToken);

    /// <summary>Moves the draft down one place; the one it swaps with is now in the Scribes' order too.</summary>
    public Task<Either<Error, Unit>> Humble(Guid draftId, string note, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Humble, note, (order, index) => index == order.Count - 1
            ? None
            : Some<List<Guid>>([.. order[..index], order[index + 1], order[index]]), cancellationToken);

    /// <summary>
    /// Burns the draft (it's kept, hidden, and can be restored) and takes it out of the Scribes' order; returns how many
    /// drafts are waiting after it. Counted after the burn is saved, so when two Scribes burn the last two at once, the
    /// later one sees none left.
    /// </summary>
    public Task<Either<Error, int>> Burn(Guid draftId, string note, CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => Calendar(cancellationToken)
            .BindAsync(calendar => calendar.Find(draftId).Match(
                Some: found => scriptorium
                    .ChangeState(draftId, DraftState.Waiting, DraftState.Burned,
                        Decision(found.Draft, ScribeAction.Burn, checkedNote, Some(found.Place), Some(calendar.AuguryPlace(found.Draft))),
                        WithoutDraft(calendar.Placements, draftId),
                        cancellationToken)
                    .BindAsync(_ => drafts.GetWaiting(cancellationToken))
                    .MapAsync(waiting => Task.FromResult(waiting.Count)),
                None: () => Task.FromResult(Left<Error, int>(ScriptoriumRepository.ChangedMeanwhile)))));

    /// <summary>Restores a burned draft, however long ago it burned: it waits again, in the Augury's order.</summary>
    public Task<Either<Error, Unit>> Restore(Guid draftId, string note, CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => drafts.Get(draftId, cancellationToken)
            .BindAsync(stored => scriptorium.GetPlacements(cancellationToken)
                .BindAsync(placements => stored.Filter(draft => draft.State == DraftState.Burned).Match(
                    Some: draft => scriptorium
                        .ChangeState(draftId, DraftState.Burned, DraftState.Waiting,
                            Decision(draft, ScribeAction.Restore, checkedNote, None, None),
                            WithoutDraft(placements, draftId),
                            cancellationToken)
                        .MapAsync(_ => Task.FromResult(unit)),
                    None: () => Task.FromResult(Left<Error, Unit>(ScriptoriumRepository.ChangedMeanwhile))))));

    /// <summary>Forgets the Scribes' order, so the Augury orders every draft.</summary>
    public Task<Either<Error, Unit>> LetTheAuguryDecide(CancellationToken cancellationToken)
        => scriptorium.GetPlacements(cancellationToken)
            .BindAsync(placements => scriptorium.SavePlacements(placements with { Order = [] }, None, cancellationToken));

    private DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    private Task<Either<Error, Unit>> Reorder(
        Guid draftId, ScribeAction action, string note, Func<List<Guid>, int, Option<List<Guid>>> reorder, CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => Calendar(cancellationToken)
            .BindAsync(calendar => calendar.Find(draftId).Match(
                Some: found => reorder(calendar.Ordered.Select(draft => draft.Id).ToList(), found.Place - 1).Match(
                    Some: moved => scriptorium.SavePlacements(
                        calendar.Placements with { Order = KeepingTheRest(moved, calendar) },
                        Decision(found.Draft, action, checkedNote, Some(found.Place), Some(calendar.AuguryPlace(found.Draft))),
                        cancellationToken),
                    // Already at the top (or bottom): nothing changes, so there's nothing to learn.
                    None: () => Task.FromResult(Right<Error, Unit>(unit))),
                None: () => Task.FromResult(Left<Error, Unit>(ScriptoriumRepository.ChangedMeanwhile)))));

    // The reordered top of the calendar, then the drafts the Scribes placed below it, still in their order: moving one
    // draft never undoes an earlier decision about another.
    private static List<Guid> KeepingTheRest(List<Guid> moved, CalendarState calendar)
    {
        var placedBelow = calendar.Ordered.Select(draft => draft.Id)
            .Where(id => calendar.Placements.Order.Contains(id) && !moved.Contains(id));
        return [.. moved, .. placedBelow];
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
                .BindAsync(waiting => rites.GetNewest(LiturgicalCalendar.ResemblanceWindow, cancellationToken)
                    .MapAsync(recent => Task.FromResult(new CalendarState(
                        LiturgicalCalendar.Order(waiting, recent, placements.Order),
                        LiturgicalCalendar.Order(waiting, recent, []),
                        placements)))));

    private ScribeDecision Decision(Draft draft, ScribeAction action, Option<string> note, Option<int> calendarPlace, Option<int> auguryPlace)
        => new()
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Action = action,
            Kind = draft.Kind,
            Title = draft.Title,
            Text = draft.Text,
            HereticalTruth = draft.HereticalTruth,
            CalendarPlace = calendarPlace,
            AuguryPlace = auguryPlace,
            AuguryQuality = draft.Augury.Map(augury => augury.Quality),
            Note = note,
            DecidedAtUtc = time.GetUtcNow().UtcDateTime,
        };

    // An empty note is no note.
    private static Either<Error, Option<string>> Note(string note)
    {
        var trimmed = note.Trim();
        if (trimmed.Length > ScribeDecision.MaxNoteLength)
            return NoteTooLong;
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
    }
}
