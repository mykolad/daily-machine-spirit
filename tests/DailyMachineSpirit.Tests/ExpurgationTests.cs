using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Scriptorium.Expurgation;
using LanguageExt;
using LanguageExt.Common;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>Erasing words from a published rite: everywhere the site keeps them, or nowhere.</summary>
public sealed class ExpurgationTests : IAsyncLifetime
{
    private const string Expurgator = "expurgator-0123456789";

    // "The Litany of the Warm Cache": Warm is its title's fifth word. "O Machine Spirit, warm thy cache, ...": warm is the
    // text's fourth.
    private static readonly RiteContent Amended = new(
        "The Litany of the Warm Cache",
        "O Machine Spirit, warm thy cache, that `npm ci` may find it full.",
        "A warm cache is one somebody already paid to fill.");

    private static readonly RedactedWord[] WarmInTitleAndText = [new(RitePart.Title, 4, "💀"), new(RitePart.Text, 3, "🦴")];

    private readonly BacklogTestbed testbed = new();
    private readonly FakePageCache pages = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public void Wording_SplitsWords_KeepingWhatsBetweenThem()
    {
        var pieces = Wording.Split("Don't run `npm ci` twice, O 💀!");

        Assert.Equal(["Don't", "run", "npm", "ci", "twice", "O"], pieces.Where(piece => piece.IsWord).Select(piece => piece.Text));
        Assert.Equal("Don't run `npm ci` twice, O 💀!", string.Concat(pieces.Select(piece => piece.Text)));
        Assert.Equal("Don't 🔥 `npm ci` twice, 🦴 💀!", Wording.Replace("Don't run `npm ci` twice, O 💀!", new Dictionary<int, string> { [1] = "🔥", [5] = "🦴" }));
        Assert.Equal("a 🔥 and a 🔥, not runs", Wording.ReplaceEverywhere("a run and a run, not runs", new Dictionary<string, string> { ["run"] = "🔥" }));
    }

    [Theory]
    [InlineData("text:12:💀", true)]
    [InlineData("title:0:🦴", true)]
    [InlineData("truth:3:🔥", true)]
    // Only the site's own emojis: a form can't put words of its own in the rite.
    [InlineData("text:12:hello", false)]
    [InlineData("text:-1:💀", false)]
    [InlineData("body:1:💀", false)]
    [InlineData("text:💀", false)]
    public void ACarriedWord_IsTakenOnlyWhenWellFormed(string carried, bool taken)
        => Assert.Equal(taken, RedactedWord.Parse(carried).IsSome);

    // The words are erased everywhere the site keeps them: the rite, its draft, the Scribes' decisions (one from before
    // an amendment, whose words differ), the Record's entries (an amendment's detail kept the old title), and a draft
    // marked as much like it. The Record says who erased how many words of which part, never the words.
    [Fact]
    public async Task Erase_ErasesTheWordsEverywhere_RecordsWhoAndWhere_AndPurgesThePages()
    {
        var number = await PublishedAfterAnAmendment();
        var resembling = Ok(await testbed.Drafts.Add(new Draft
        {
            Id = Guid.NewGuid(),
            State = DraftState.Waiting,
            Writing = new RiteWriting { Kind = RiteKind.Prayer, Title = "The Second Litany", Text = "Again.", HereticalTruth = "Twice." },
            Resembles = Some(Amended.Title),
        }, CancellationToken.None));
        var rite = Found(await Expurgations().Find(number, CancellationToken.None));

        var erased = Ok(await Expurgations().Erase(number, rite.Version, WarmInTitleAndText, Expurgator, CancellationToken.None));

        var reads = Found(await Expurgations().Find(number, CancellationToken.None)).Rite.Writing;
        Assert.Equal("The Litany of the 💀 Cache", reads.Title);
        Assert.Equal("O Machine Spirit, 🦴 thy cache, that `npm ci` may find it full.", reads.Text);
        Assert.Equal(Amended.HereticalTruth, reads.HereticalTruth);
        Assert.Equal(Some(reads.Title), erased.Map(rite => rite.Writing.Title));
        // The title's Warm is gone from every copy; the truth keeps its own "warm", which wasn't chosen.
        Assert.All(await testbed.AllDocuments(), document => Assert.DoesNotContain("Warm", document));
        Assert.Equal(Some("The Litany of the 💀 Cache"), Ok(await testbed.Drafts.Get(resembling.Id, CancellationToken.None)).Bind(draft => draft.Resembles));
        var entry = Assert.Single(Ok(await testbed.Audit.GetNewest(20, CancellationToken.None)), entry => entry.Action == AuditAction.Expurgate);
        Assert.Equal((Expurgator, Some(number), "The Litany of the 💀 Cache"), (entry.Actor, entry.RiteNumber, entry.Title));
        Assert.Equal(Some("1 word of its title, 1 word of its text"), entry.Detail);
        Assert.Equal([Paths.Today, Paths.Rite(number), Paths.SearchIndex], Assert.Single(pages.Purges));
    }

    // Chosen on a page shown before another erasure, the places would land on other words: nothing changes.
    [Fact]
    public async Task Erase_FromWordsThatChangedSince_ChangesNothing()
    {
        var number = await PublishedAfterAnAmendment();
        var before = await testbed.AllDocuments();

        var refused = Failed(await Expurgations().Erase(number, "0000000000000000", WarmInTitleAndText, Expurgator, CancellationToken.None));

        Assert.Equal(Functions.Scriptorium.Expurgation.Expurgations.WordsChanged, refused);
        Assert.Equal(before.Order(), (await testbed.AllDocuments()).Order());
        Assert.Empty(pages.Purges);
    }

    // A vote changes the rite's document, not its words: the erasure reads it again and goes ahead.
    [Fact]
    public async Task Erase_WhenAVoteLandsMeanwhile_TriesAgain()
    {
        var number = await PublishedAfterAnAmendment();
        var version = Found(await Expurgations().Find(number, CancellationToken.None)).Version;
        var voted = new VotingMeanwhile(testbed.Expurgation, () => testbed.Votes.Vote(number, "voter-key", Some(Reaction.Blessed), CancellationToken.None));

        Ok(await testbed.Expurgations(voted, pages).Erase(number, version, WarmInTitleAndText, Expurgator, CancellationToken.None));

        Assert.Equal(2, voted.Reads);
        Assert.Equal("The Litany of the 💀 Cache", Found(await Expurgations().Find(number, CancellationToken.None)).Rite.Writing.Title);
    }

    [Fact]
    public async Task Erase_AWordTheRiteHasnt_OrNone_ChangesNothing()
    {
        var number = await PublishedAfterAnAmendment();
        var version = Found(await Expurgations().Find(number, CancellationToken.None)).Version;

        Assert.Equal(Functions.Scriptorium.Expurgation.Expurgations.NoSuchWord, Failed(await Expurgations().Erase(number, version, [new(RitePart.Title, 6, "💀")], Expurgator, CancellationToken.None)));
        Assert.Equal(Functions.Scriptorium.Expurgation.Expurgations.NothingChosen, Failed(await Expurgations().Erase(number, version, [], Expurgator, CancellationToken.None)));
        Assert.Equal(Amended.Title, Found(await Expurgations().Find(number, CancellationToken.None)).Rite.Writing.Title);
    }

    [Fact]
    public async Task Erase_ARiteThatIsntThere_ErasesNothing()
    {
        Assert.True(Ok(await Expurgations().Erase(7, "0000000000000000", WarmInTitleAndText, Expurgator, CancellationToken.None)).IsNone);
        Assert.True(Ok(await testbed.Expurgation.FindWordsOf(7, CancellationToken.None)).IsNone);
        Assert.Empty(pages.Purges);
    }

    // A batch takes at most 100 operations: rather than erase some copies and not others, none.
    [Fact]
    public async Task Erase_MoreCopiesThanABatchTakes_IsRefused()
    {
        var copies = Enumerable.Range(0, ExpurgationRepository.MaxCopies + 1)
            .Select(copy => new WordsKept($"copy-{copy}", "etag", new Dictionary<string, string> { ["/title"] = "💀" }))
            .ToList();

        var refused = Failed(await testbed.Expurgation.Erase(copies, new AuditEntry { Id = Guid.NewGuid() }, CancellationToken.None));

        Assert.Equal(ExpurgationRepository.TooManyCopies, refused);
    }

    // Written, amended (the decision keeps the old words, the Record's detail the old title), anointed and published:
    // every kind of copy there is.
    private async Task<int> PublishedAfterAnAmendment()
    {
        var draft = await testbed.AddDraft("Litany of the Cold Cache", 0.9f);
        var amendable = Ok(await testbed.Amendments().FindWaiting(draft.Id, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("Not amendable."));
        Ok(await testbed.Amendments().SaveAndJudgeAgain(draft.Id, amendable.ETag, Amended, "Kinder.", BacklogTestbed.Tester, CancellationToken.None));
        Ok(await testbed.Scribes().Anoint(draft.Id, "", BacklogTestbed.Tester, CancellationToken.None));
        return Ok(await testbed.Publisher().PublishToday(CancellationToken.None)).Rite.Number;
    }

    private Expurgations Expurgations() => testbed.Expurgations(testbed.Expurgation, pages);

    private static ExpurgatableRite Found(Either<Error, Option<ExpurgatableRite>> found)
        => Ok(found).IfNone(() => throw new Xunit.Sdk.XunitException("No such rite."));

    /// <summary>Votes on the rite after the first read of its copies, so their version is out of date when erased.</summary>
    private sealed class VotingMeanwhile : IExpurgationRepository
    {
        private readonly IExpurgationRepository inner;
        private readonly Func<Task<Either<Error, Option<ReactionCounts>>>> vote;

        public VotingMeanwhile(IExpurgationRepository inner, Func<Task<Either<Error, Option<ReactionCounts>>>> vote)
        {
            this.inner = inner;
            this.vote = vote;
        }

        public int Reads { get; private set; }

        public async Task<Either<Error, Option<List<WordsKept>>>> FindWordsOf(int number, CancellationToken cancellationToken)
        {
            var found = await inner.FindWordsOf(number, cancellationToken);
            if (++Reads == 1)
            {
                Ok(await vote());
            }
            return found;
        }

        public Task<Either<Error, Unit>> Erase(IReadOnlyList<WordsKept> changed, AuditEntry audit, CancellationToken cancellationToken)
            => inner.Erase(changed, audit, cancellationToken);
    }
}
