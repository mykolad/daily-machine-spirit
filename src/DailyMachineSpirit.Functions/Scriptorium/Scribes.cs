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
/// until it does. The calendar is full at <see cref="FullAt"/> rites, as many as a refill keeps waiting.
/// </summary>
public sealed record ScriptoriumView(List<CalendarEntry> Calendar, List<ScribeDecision> Decisions, DateTime NowUtc, int FullAt)
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

    /// <summary>Exalting the first anointed draft or humbling the last: a page shown before another Scribe moved it.</summary>
    public static readonly Error NothingToMove = Error.New("The draft is already at that end of the anointed.");

    private readonly IDraftRepository drafts;
    private readonly IRiteRepository rites;
    private readonly IScriptoriumRepository scriptorium;
    private readonly IOptions<AuguryOptions> augury;
    private readonly IOptions<ScriptoriumPagesOptions> pages;
    private readonly IOptions<BacklogOptions> backlog;
    private readonly TimeProvider time;

    public Scribes(
        IDraftRepository drafts,
        IRiteRepository rites,
        IScriptoriumRepository scriptorium,
        IOptions<AuguryOptions> augury,
        IOptions<ScriptoriumPagesOptions> pages,
        IOptions<BacklogOptions> backlog,
        TimeProvider time)
    {
        this.drafts = drafts;
        this.rites = rites;
        this.scriptorium = scriptorium;
        this.augury = augury;
        this.pages = pages;
        this.backlog = backlog;
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
                        return Task.FromResult(new ScriptoriumView(entries, decisions, now, backlog.Value.DraftsWaiting));
                    })));
    }

    /// <summary>The Ashes: the drafts burned most recently, the newest first.</summary>
    public Task<Either<Error, List<Draft>>> Ashes(CancellationToken cancellationToken)
        => scriptorium.GetBurned(pages.Value.AshesShown, cancellationToken);

    /// <summary>The draft, if it's burned: what the calendar's message names after a burn, the draft having left it.</summary>
    public Task<Either<Error, Option<Draft>>> FindBurned(Guid draftId, CancellationToken cancellationToken)
        => drafts.Get(draftId, cancellationToken)
            .MapAsync(found => Task.FromResult(found.Filter(draft => draft.State == DraftState.Burned)));

    /// <summary>
    /// Anoints the draft: it joins the Scribes' order after the drafts anointed before it (first, when none are), so it's
    /// published ahead of every draft the Augury orders, which keeps ordering the rest.
    /// </summary>
    public Task<Either<Error, Unit>> Anoint(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Anoint, note, scribe, (anointed, index) => index >= 0
            ? ScriptoriumRepository.ChangedMeanwhile
            : Right<Error, List<Guid>>([.. anointed, draftId]), cancellationToken);

    /// <summary>Moves an anointed draft up one place among the anointed.</summary>
    public Task<Either<Error, Unit>> Exalt(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Exalt, note, scribe, (anointed, index) => index switch
        {
            < 0 => ScriptoriumRepository.ChangedMeanwhile,
            0 => NothingToMove,
            _ => Right<Error, List<Guid>>([.. anointed[..(index - 1)], anointed[index], anointed[index - 1], .. anointed[(index + 1)..]]),
        }, cancellationToken);

    /// <summary>Moves an anointed draft down one place among the anointed.</summary>
    public Task<Either<Error, Unit>> Humble(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Humble, note, scribe, (anointed, index) => index switch
        {
            < 0 => ScriptoriumRepository.ChangedMeanwhile,
            _ when index == anointed.Count - 1 => NothingToMove,
            _ => Right<Error, List<Guid>>([.. anointed[..index], anointed[index + 1], anointed[index], .. anointed[(index + 2)..]]),
        }, cancellationToken);

    /// <summary>Releases an anointed draft from the Scribes' order: the Augury orders it with the rest.</summary>
    public Task<Either<Error, Unit>> Release(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Reorder(draftId, ScribeAction.Release, note, scribe, (anointed, index) => index < 0
            ? ScriptoriumRepository.ChangedMeanwhile
            : Right<Error, List<Guid>>(anointed.Where(id => id != draftId).ToList()), cancellationToken);

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

    /// <summary>Restores a burned draft, however long ago it burned: it waits again, in the Augury's order.</summary>
    public Task<Either<Error, Unit>> Restore(Guid draftId, string note, string scribe, CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => drafts.Get(draftId, cancellationToken)
            .BindAsync(stored => scriptorium.GetPlacements(cancellationToken)
                .BindAsync(placements => stored.Filter(draft => draft.State == DraftState.Burned).Match(
                    Some: draft => scriptorium
                        .ChangeState(draftId, DraftState.Burned, DraftState.Waiting,
                            Decision(draft, ScribeAction.Restore, checkedNote, None),
                            WithoutDraft(placements, draftId),
                            AuditEntries.Of(time, scribe, AuditAction.Restore, draft, None),
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

    // The new Scribes' order, from the anointed drafts still waiting (the calendar's first places, in their order) and
    // where the draft is among them (-1 when it isn't). Refused (already at an end, or not anointed), nothing changes,
    // so there's nothing to learn, and the page says why.
    private Task<Either<Error, Unit>> Reorder(
        Guid draftId, ScribeAction action, string note, string scribe, Func<List<Guid>, int, Either<Error, List<Guid>>> reorder,
        CancellationToken cancellationToken)
        => Note(note).BindAsync(checkedNote => Calendar(cancellationToken)
            .BindAsync(calendar => calendar.Find(draftId).Match(
                Some: found =>
                {
                    var anointed = calendar.Anointed();
                    return reorder(anointed, anointed.IndexOf(draftId)).Match(
                        // Every anointed draft must still wait (it was read waiting, and the new order places it), and so
                        // must the one acted on, a released one too: published meanwhile, there's nothing to release.
                        Right: order =>
                        {
                            var after = order.IndexOf(draftId) + 1;
                            var placeAfter = after > 0 ? Some(after) : None;
                            return scriptorium.SavePlacements(
                                Placed(calendar.Placements, order, draftId, scribe),
                                Decision(found.Draft, action, checkedNote, Some(calendar.PlacesOf(found))) with { CalendarPlaceAfter = placeAfter },
                                [.. order, draftId],
                                AuditEntries.Of(time, scribe, Audited(action), found.Draft,
                                    $"from #{found.Place} to {placeAfter.Map(place => $"#{place}").IfNone("the Augury's order")}"),
                                cancellationToken);
                        },
                        Left: refused => Task.FromResult(Left<Error, Unit>(refused)));
                },
                None: () => Task.FromResult(Left<Error, Unit>(ScriptoriumRepository.ChangedMeanwhile)))));

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
            Ailment = draft.Writing.Ailment,
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
        ScribeAction.Humble => AuditAction.Humble,
        _ => AuditAction.Release,
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

        /// <summary>The anointed drafts still waiting, in the Scribes' order: the calendar's first places.</summary>
        public List<Guid> Anointed() => Ordered.Select(draft => draft.Id).Where(Placements.Order.Contains).ToList();

        public int AuguryPlace(Draft draft) => AuguryOrder.FindIndex(other => other.Id == draft.Id) + 1;

        public DraftPlaces PlacesOf((Draft Draft, int Place) found)
            => new(found.Place, AuguryPlace(found.Draft), LiturgicalCalendar.RanksByQuality(found.Draft, AuguryOrder));
    }
}
