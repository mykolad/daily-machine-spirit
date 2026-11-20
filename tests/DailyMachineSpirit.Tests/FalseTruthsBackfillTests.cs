using System.Text;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.FalseTruths;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// False truths for what was written before them (#199): proposed for a published rite, until a Scribe accepts them;
/// given to a waiting draft, which the Scribes read before it's published.
/// </summary>
public sealed class FalseTruthsBackfillTests : IAsyncLifetime
{
    private static readonly FalseTruths Written = new(FakeChatClients.FirstFalseTruth, FakeChatClients.SecondFalseTruth);

    private readonly BacklogTestbed testbed = new();
    private readonly FakePageCache pages = new();

    public ValueTask InitializeAsync() => testbed.InitializeAsync();

    public ValueTask DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task Pending_NamesTheRitesAndWaitingDraftsWithout()
    {
        var number = await Published("Litany of the Cold Cache");
        var waiting = await testbed.AddDraft("The Rite of Re-Run", 0.5f);
        Ok(await testbed.Drafts.Add(waiting with { Id = Guid.NewGuid(), Writing = waiting.Writing with { FalseTruths = Written } }, CancellationToken.None));

        var pending = Ok(await Backfill().Pending(CancellationToken.None));

        Assert.Equal([$"rite:{number}", $"draft:{waiting.Id:D}"], pending);
    }

    // The rite keeps its plain seal until a Scribe accepts them: only the proposal is saved, and only the answer's
    // false truths, whatever else the model wrote.
    [Fact]
    public async Task ARite_GetsThemProposed_AndKeepsItsWords()
    {
        var number = await Published("Litany of the Cold Cache");
        testbed.Models.Answers(Sol, FakeChatClients.Answer("A Title the Model Made Up", "Other words.", "Another truth."));

        Ok(await Backfill().Write($"rite:{number}", CancellationToken.None));

        var proposal = Assert.Single(Ok(await Backfill().Proposals(CancellationToken.None)));
        Assert.Equal((number, "Litany of the Cold Cache", Written), (proposal.Number, proposal.Title, proposal.Proposed));
        Assert.True((await RiteNumbered(number)).Writing.FalseTruths.IsNone);
        Assert.Empty(Ok(await Backfill().Pending(CancellationToken.None)));
        Assert.Contains("Litany of the Cold Cache", testbed.Models.Requests.Single().Messages.Last().Text);
    }

    [Fact]
    public async Task AWaitingDraft_GetsThemAsANewOneWould()
    {
        var draft = await testbed.AddDraft("The Rite of Re-Run", 0.5f);
        testbed.Models.Answers(Sol, FakeChatClients.Answer("The Rite of Re-Run", "Words.", "Truth."));

        Ok(await Backfill().Write($"draft:{draft.Id:D}", CancellationToken.None));

        Assert.Equal(Some(Written), Ok(await testbed.Drafts.Get(draft.Id, CancellationToken.None)).Bind(found => found.Writing.FalseTruths));
    }

    // A message delivered twice writes over nothing: the first proposal stands.
    [Fact]
    public async Task AMessageAgain_KeepsTheFirstProposal()
    {
        var number = await Published("Litany of the Cold Cache");
        testbed.Models.Answers(Sol, FakeChatClients.Answer("T", "W.", "T.", "First one.", "Second one."), FakeChatClients.Answer("T", "W.", "T.", "Other one.", "Yet another."));

        Ok(await Backfill().Write($"rite:{number}", CancellationToken.None));
        Ok(await Backfill().Write($"rite:{number}", CancellationToken.None));

        Assert.Equal(new FalseTruths("First one.", "Second one."), Assert.Single(Ok(await Backfill().Proposals(CancellationToken.None))).Proposed);
    }

    // Accepted as the Scribe left them, the rite asks its readers to guess, and its pages are fresh.
    [Fact]
    public async Task Accepting_GivesTheRiteThem_AndPurgesItsPages()
    {
        var number = await Proposed("Litany of the Cold Cache");

        Assert.True(Ok(await Backfill().Accept(number, "A warmer disk would help.", "The cache is too small.", CancellationToken.None)));

        Assert.Equal(Some(new FalseTruths("A warmer disk would help.", "The cache is too small.")), (await RiteNumbered(number)).Writing.FalseTruths);
        Assert.Empty(Ok(await Backfill().Proposals(CancellationToken.None)));
        Assert.Equal([Paths.Rite(number), Paths.Today], Assert.Single(pages.Purges));
    }

    // A Scribe's words keep the rules a rite's do; refused, nothing changes.
    [Theory]
    [InlineData("A warmer disk would help.", "")]
    [InlineData("The chatbot cleared the cache.", "The cache is too small.")]
    public async Task Accepting_WordsThatBreakTheRules_IsRefused(string first, string second)
    {
        var number = await Proposed("Litany of the Cold Cache");

        var refused = await Backfill().Accept(number, first, second, CancellationToken.None);

        Assert.IsType<AmendmentRefused>(Assert.Single(refused.LeftToSeq()));
        Assert.True((await RiteNumbered(number)).Writing.FalseTruths.IsNone);
        Assert.Single(Ok(await Backfill().Proposals(CancellationToken.None)));
    }

    [Fact]
    public async Task WritingAgain_DropsTheProposal_AndAsksForAnother()
    {
        var number = await Proposed("Litany of the Cold Cache");

        var response = await Function().Again(Post(""), number, CancellationToken.None);

        Assert.Equal([FalseTruthsBackfill.RiteMessage(number)], response.ToWrite);
        Assert.Empty(Ok(await Backfill().Proposals(CancellationToken.None)));
    }

    // Starting the backfill asks for every rite and draft without them, one message each, and changes nothing itself.
    [Fact]
    public async Task ThePage_ShowsTheProposals_AndStartingIt_QueuesWhatsMissing()
    {
        var number = await Proposed("Litany of the Cold Cache");
        var draft = await testbed.AddDraft("The Rite of Re-Run", 0.5f);

        var page = Assert.IsType<ContentResult>(await Function().View(Get(), CancellationToken.None)).Content ?? string.Empty;
        var started = await Function().Write(Post(""), CancellationToken.None);

        Assert.Contains($"""action="/scriptorium/false-truths/{number}/accept">""", page);
        Assert.Contains(FakeChatClients.FirstFalseTruth, page);
        Assert.Contains("1 rite or draft has none yet.", page);
        Assert.Equal([$"draft:{draft.Id:D}"], started.ToWrite);
        Assert.Equal("/scriptorium/false-truths?done=writing", Assert.IsType<RedirectResult>(started.Result).Url);
    }

    private FalseTruthsBackfill Backfill() => testbed.FalseTruthsBackfill(pages);

    private FalseTruthsFunction Function() => new(Backfill(), new EveryoneIsScribeSignIn(), NullLogger<FalseTruthsFunction>.Instance);

    private async Task<int> Published(string title)
    {
        await testbed.AddDraft(title, 0.9f);
        return Ok(await testbed.Publisher().PublishToday(CancellationToken.None)).Rite.Number;
    }

    private async Task<int> Proposed(string title)
    {
        var number = await Published(title);
        testbed.Models.Answers(Sol, FakeChatClients.Answer(title, "Words.", "Truth."));
        Ok(await Backfill().Write($"rite:{number}", CancellationToken.None));
        return number;
    }

    private async Task<Rite> RiteNumbered(int number)
        => Assert.Single(Ok(await testbed.Rites.GetByNumbers([number], CancellationToken.None)));

    private static HttpRequest Get()
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "GET";
        return request;
    }

    private static HttpRequest Post(string body)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "POST";
        request.Host = new HostString("dailymachinespirit.fyi");
        request.Headers["Sec-Fetch-Site"] = "same-origin";
        request.ContentType = "application/x-www-form-urlencoded";
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return request;
    }
}
