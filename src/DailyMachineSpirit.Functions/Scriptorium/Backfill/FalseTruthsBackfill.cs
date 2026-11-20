using System.Globalization;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Pages;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Backfill;

/// <summary>
/// False truths for everything written before them (#199), one rite or draft a message, since each is a model's answer:
/// a waiting draft gets them as a new one would (the Scribes read every draft before it's published), a published rite
/// gets them proposed, for a Scribe to accept, amend or have written again; until then it keeps the plain seal.
/// </summary>
public sealed class FalseTruthsBackfill
{
    public const string QueueName = "false-truths";

    private const string RitePrefix = "rite:";
    private const string DraftPrefix = "draft:";

    // A rite from before ailments (#115) has none, and accepting false truths doesn't give it one; the checks, which ask
    // every rite for one, are shown this in its place.
    private const string NoAilment = "None, written before ailments.";

    private readonly IFalseTruthsRepository falseTruths;
    private readonly IDraftRepository drafts;
    private readonly RiteWriter writer;
    private readonly IPageCache pages;
    private readonly TimeProvider time;

    public FalseTruthsBackfill(IFalseTruthsRepository falseTruths, IDraftRepository drafts, RiteWriter writer, IPageCache pages, TimeProvider time)
    {
        this.falseTruths = falseTruths;
        this.drafts = drafts;
        this.writer = writer;
        this.pages = pages;
        this.time = time;
    }

    /// <summary>A message for each rite and waiting draft that has neither false truths nor a proposal.</summary>
    public async Task<Either<Error, IReadOnlyList<string>>> Pending(CancellationToken cancellationToken)
    {
        var (withoutRites, withoutDrafts) = (falseTruths.RitesWithout(cancellationToken), falseTruths.DraftsWithout(cancellationToken));
        var (numbers, ids) = (await withoutRites, await withoutDrafts);
        return from rite in numbers
               from draft in ids
               select (IReadOnlyList<string>)[.. rite.Select(RiteMessage), .. draft.Select(id => DraftPrefix + id.ToString("D"))];
    }

    public static string RiteMessage(int number) => RitePrefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes the false truths a message asks for. One that has them or a proposal by now (a message delivered twice, a
    /// Scribe's amendment), is published, or is gone, is done with: the model isn't asked.
    /// </summary>
    public async Task<Either<Error, Unit>> Write(string message, CancellationToken cancellationToken)
    {
        if (message.StartsWith(RitePrefix, StringComparison.Ordinal)
            && int.TryParse(message[RitePrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return await (await falseTruths.RiteWithout(number, cancellationToken)).Match(
                Right: found => found.Match(
                    Some: rite => ProposeFor(rite, cancellationToken),
                    None: () => Task.FromResult(Right<Error, Unit>(unit))),
                Left: error => Task.FromResult(Left<Error, Unit>(error)));
        }
        if (message.StartsWith(DraftPrefix, StringComparison.Ordinal) && Guid.TryParse(message[DraftPrefix.Length..], out var draftId))
        {
            return await (await drafts.Get(draftId, cancellationToken)).Match(
                Right: found => found.Filter(draft => draft.State == DraftState.Waiting && draft.Writing.FalseTruths.IsNone).Match(
                    Some: draft => GiveTo(draft, cancellationToken),
                    None: () => Task.FromResult(Right<Error, Unit>(unit))),
                Left: error => Task.FromResult(Left<Error, Unit>(error)));
        }
        return Error.New($"\"{message}\" names no rite or draft.");
    }

    public Task<Either<Error, IReadOnlyList<ProposedFalseTruths>>> Proposals(CancellationToken cancellationToken)
        => falseTruths.Proposals(cancellationToken);

    /// <summary>
    /// The rite takes these false truths, the proposal or the Scribe's words for it, if they keep the rules a rite's words
    /// keep (<see cref="RiteChecks"/>); its page shows them from then on, and the Record says who gave them. False when
    /// the proposal is gone meanwhile.
    /// </summary>
    public async Task<Either<Error, bool>> Accept(int number, string first, string second, ScribeId scribe, CancellationToken cancellationToken)
        => await (await falseTruths.Proposal(number, cancellationToken)).Match(
            Right: found => found.Match(
                Some: proposal => AcceptFor(proposal, first, second, scribe, cancellationToken),
                None: () => Task.FromResult(Right<Error, bool>(false))),
            Left: error => Task.FromResult(Left<Error, bool>(error)));

    /// <summary>Drops the rite's proposal, for it to be written again; false when it had none by now.</summary>
    public Task<Either<Error, bool>> Withdraw(int number, CancellationToken cancellationToken)
        => falseTruths.Withdraw(number, cancellationToken);

    private async Task<Either<Error, Unit>> ProposeFor(Rite rite, CancellationToken cancellationToken)
    {
        var written = await writer.WriteFalseTruths(rite.Writing.Kind, Words(rite.Writing), cancellationToken);
        return (await written.BindAsync(proposed => falseTruths.Propose(rite.Number, proposed, cancellationToken))).Map(_ => unit);
    }

    private async Task<Either<Error, Unit>> GiveTo(Draft draft, CancellationToken cancellationToken)
    {
        var written = await writer.WriteFalseTruths(draft.Writing.Kind, Words(draft.Writing), cancellationToken);
        return (await written.BindAsync(given => falseTruths.GiveDraft(draft.Id, given, cancellationToken))).Map(_ => unit);
    }

    // A Scribe's words for them are checked as the rite's own: a refusal is theirs to fix on the page.
    private async Task<Either<Error, bool>> AcceptFor(ProposedFalseTruths proposal, string first, string second, ScribeId scribe, CancellationToken cancellationToken)
    {
        var words = new RiteContent(proposal.Title, proposal.Text, proposal.HereticalTruth, first, second, proposal.Ailment.IfNone(NoAilment));
        var checkedWords = RiteChecks.Check(words, proposal.Kind)
            .Bind(content => RiteChecks.FalseTruthsOf(content).ToEither(Error.New("Write both false truths.")))
            .MapLeft(error => (Error)new AmendmentRefused(error.Message));
        var taken = await checkedWords.BindAsync(accepted => falseTruths.Accept(proposal.Number, accepted, Entry(proposal, accepted, scribe), cancellationToken));
        if (taken.Exists(accepted => accepted))
        {
            // The guess is on the rite's page, and on Today's while it's the newest.
            await pages.Purge([Paths.Rite(proposal.Number), Paths.Today], cancellationToken);
        }
        return taken;
    }

    private AuditEntry Entry(ProposedFalseTruths proposal, FalseTruths accepted, ScribeId scribe) => new()
    {
        Id = Guid.NewGuid(),
        AtUtc = time.GetUtcNow().UtcDateTime,
        Actor = scribe,
        Action = AuditAction.AcceptFalseTruths,
        RiteNumber = proposal.Number,
        Title = proposal.Title,
        Detail = accepted == proposal.Proposed ? None : Some("amended"),
    };

    // The rite's words, as the writer reads them; one from before ailments (#115) has none.
    private static RiteContent Words(RiteWriting writing)
        => new(writing.Title, writing.Text, writing.HereticalTruth, string.Empty, string.Empty, writing.Ailment.IfNone(string.Empty));
}
