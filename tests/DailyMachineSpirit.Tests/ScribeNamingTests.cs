using System.Text.Json;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Tests.BacklogTestbed;
using static DailyMachineSpirit.Tests.Expect;
using static DailyMachineSpirit.Tests.ScriptoriumRequests;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

/// <summary>The names the Scribes choose for the Record (#84), against the Cosmos DB emulator with fake models.</summary>
public sealed class ScribeNamingTests : IAsyncLifetime
{
    private const string Ferrum = "7335d417-61da-459d-899c-0a1f8f6f0d3b";
    private const string Cache = "1a2b3c4d-0000-0000-0000-00000000cafe";

    private readonly BacklogTestbed testbed = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    // Chosen, the name is the Scribe's on every entry, past ones too, and choosing it is recorded with the name before.
    [Fact]
    public async Task AChosenName_IsShownForTheScribe_AndRecorded()
    {
        var naming = testbed.ScribeNaming();
        Ok(await testbed.Audit.Add(AuditEntries.Of(testbed.Time, Ferrum, AuditAction.Summon, None, None), CancellationToken.None));

        Assert.Equal("Brother Ferrum", Ok(await naming.Choose(Ferrum, "  Brother   Ferrum \n", CancellationToken.None)));
        testbed.Time.Advance(TimeSpan.FromMinutes(1));
        Ok(await naming.Choose(Ferrum, "Magos Ferrum", CancellationToken.None));

        Assert.Equal(Some("Magos Ferrum"), Ok(await naming.NameOf(Ferrum, CancellationToken.None)));
        var html = ScriptoriumPage.RenderRecord(
            Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)), Ok(await naming.All(CancellationToken.None)), None);
        Assert.Contains("Magos Ferrum summoned new rites", html);
        Assert.Contains("Magos Ferrum chose the name “Magos Ferrum” <span class=\"when\">(was “Brother Ferrum”)</span>", html);
        Assert.Contains("Magos Ferrum chose the name “Brother Ferrum”</li>", html);
    }

    // Their own name again changes nothing, so nothing is recorded.
    [Fact]
    public async Task TheSameNameAgain_ChangesNothing()
    {
        var naming = testbed.ScribeNaming();
        Ok(await naming.Choose(Ferrum, "Brother Ferrum", CancellationToken.None));

        Ok(await naming.Choose(Ferrum, "Brother Ferrum", CancellationToken.None));

        Assert.Single(Ok(await testbed.Audit.GetNewest(10, CancellationToken.None)), entry => entry.Action == AuditAction.ChooseName);
    }

    // Two Scribes never share a name, whatever its case; and none takes a name the Record gives to others.
    [Theory]
    [InlineData("sister cache", "Another Scribe goes by that name. Choose another.")]
    [InlineData("the machine spirit", "The Record gives that name to others. Choose another.")]
    [InlineData("A Scribe on Staging", "The Record gives that name to others. Choose another.")]
    [InlineData("A Scribe (…d0f3b)", "The Record gives that name to others. Choose another.")]
    [InlineData(" \t ", "Write a name first.")]
    [InlineData("scribe@example.com", "A name or an alias, never an email address: the Record isn't a place for one.")]
    [InlineData("A name far, far longer than the forty characters allowed", "A name can be at most 40 characters.")]
    public async Task ANameThatCantBeTaken_IsRefusedWithWhy(string name, string why)
    {
        var naming = testbed.ScribeNaming();
        Ok(await naming.Choose(Cache, "Sister Cache", CancellationToken.None));

        var refused = Failed(await naming.Choose(Ferrum, name, CancellationToken.None));

        Assert.Equal(new NameRefused(why), refused);
        Assert.Equal(Option<string>.None, Ok(await naming.NameOf(Ferrum, CancellationToken.None)));
    }

    // Staging's one shared Scribe takes a name like any other, so the feature can be tried there.
    [Fact]
    public async Task OnStaging_TheSharedScribe_TakesAName()
    {
        var naming = testbed.ScribeNaming();

        Ok(await naming.Choose(AuditEntry.StagingScribe, "Brother Ferrum", CancellationToken.None));

        Assert.Equal(Some("Brother Ferrum"), Ok(await naming.NameOf(AuditEntry.StagingScribe, CancellationToken.None)));
    }

    // Two Scribes choosing at once: the second, saving over a read made before the first's save, is told to try again.
    [Fact]
    public async Task ASave_FromAStaleRead_IsRefused()
    {
        var read = Ok(await testbed.ScribeNames.GetAll(CancellationToken.None));
        Ok(await testbed.ScribeNaming().Choose(Cache, "Sister Cache", CancellationToken.None));

        var late = await testbed.ScribeNames.SaveUnlessChangedSince(read, [new ScribeName(Ferrum, "Sister Cache")], Entry(Ferrum), CancellationToken.None);

        Assert.Equal(ScriptoriumRepository.ChangedMeanwhile, Failed(late));
    }

    // The model's names, but never one taken or the Record's own; the site's own fill the rest.
    [Fact]
    public async Task Suggestions_AreThreeChoosableNames_NoneTaken()
    {
        testbed.Models.Answers(Namer, Names("Sister Cache", "The Machine Spirit", "Cantor Kernel"));

        var suggested = await testbed.NameSuggester().Suggest(["Sister Cache"], CancellationToken.None);

        Assert.Equal(["Cantor Kernel", "Brother Ferrum", "Deacon Null"], suggested);
        Assert.Contains("[\"Sister Cache\"]", testbed.Models.Requests.Single().Messages.Last().Text);
    }

    // Suggestions are tidied as a typed name is before they're compared: a spacing of a taken name, or of one already
    // suggested, isn't offered, nor is an email.
    [Fact]
    public async Task Suggestions_AreComparedAsTheyWouldBeSaved()
    {
        testbed.Models.Answers(Namer, Names("Sister  Cache", "Cantor Kernel", "cantor   kernel", "scribe@example.com"));

        var suggested = await testbed.NameSuggester().Suggest(["Sister Cache"], CancellationToken.None);

        Assert.Equal(["Cantor Kernel", "Brother Ferrum", "Deacon Null"], suggested);
    }

    // However many names are taken, there are three more: past its own and every title with every word, the site
    // numbers its own.
    [Fact]
    public async Task Suggestions_NeverRunOut()
    {
        string[] titles = ["Brother", "Sister", "Deacon", "Magos", "Cantor", "Lector", "Acolyte", "Prior", "Archivist", "Vicar"];
        string[] words = ["Kernel", "Heap", "Commit", "Socket", "Buffer", "Daemon", "Cron", "Thread", "Mutex", "Patch"];
        List<string> taken =
        [
            "Brother Ferrum", "Sister Cache", "Deacon Null", "Magos Rerun", "Cantor Retry", "Lector Semicolon",
            "Acolyte Timeout", "Prior Rebase", "Archivist Flake", "Vicar Restart", "Abbess Lockfile", "Brother Stacktrace",
            .. titles.SelectMany(title => words.Select(word => $"{title} {word}")),
        ];
        testbed.Models.Fails(Namer, 1);

        Assert.Equal(["Brother Ferrum II", "Sister Cache II", "Deacon Null II"], await testbed.NameSuggester().Suggest(taken, CancellationToken.None));
    }

    [Fact]
    public async Task Suggestions_WhenTheModelCantAnswer_AreTheSitesOwn()
    {
        testbed.Models.Fails(Namer, 1);

        Assert.Equal(["Brother Ferrum", "Sister Cache", "Deacon Null"], await testbed.NameSuggester().Suggest([], CancellationToken.None));
    }

    // The page offers the suggestions, the first in the field to take as it is or change; each suggestion's link puts it
    // in the field, carrying the others so the model isn't asked again.
    [Fact]
    public async Task ThePage_OffersSuggestions_ToTakeOrChange()
    {
        testbed.Models.Answers(Namer, Names("Cantor Kernel", "Abbot Commit", "Sister Heap"));

        var html = Html(await Function(Ferrum).Page(Get(""), CancellationToken.None), 200);
        var followed = Html(await Function(Ferrum).Page(Get("?name=Abbot%20Commit&s=Cantor%20Kernel&s=Abbot%20Commit&s=Sister%20Heap"), CancellationToken.None), 200);

        Assert.Contains("The Record shows you as “A Scribe (…6f0d3b)”.", html);
        Assert.Contains("""<input type="text" id="scribe-name" name="name" value="Cantor Kernel" """, html);
        Assert.Contains("""<a class="button-link" href="/scriptorium/name?name=Abbot%20Commit&amp;s=Cantor%20Kernel&amp;s=Abbot%20Commit&amp;s=Sister%20Heap#scribe-name">Abbot Commit</a>""", html);
        Assert.Contains("""<input type="hidden" name="s" value="Sister Heap">""", html);
        // Asking for others is no name: it stands apart from the list, with its own icon.
        Assert.Contains("""Sister Heap</a></li></ul>""", html);
        Assert.Contains($"""<p class="suggest-others"><a class="button-link" href="/scriptorium/name">{ScriptoriumIcons.Refresh}Suggest Others</a></p>""", html);
        Assert.Contains("""value="Abbot Commit" """, followed);
        Assert.Single(testbed.Models.Requests);
    }

    // A suggestion another Scribe took since the page was shown isn't offered again, even on the page that refuses it: a
    // name of the site's own takes its place, without asking the model.
    [Fact]
    public async Task ASuggestionTakenMeanwhile_IsReplaced_WithoutAskingTheModel()
    {
        Ok(await testbed.ScribeNaming().Choose(Cache, "Abbot Commit", CancellationToken.None));
        var carried = "&s=Cantor%20Kernel&s=Abbot%20Commit&s=Sister%20Heap";

        var followed = Html(await Function(Ferrum).Page(Get($"?name=Cantor%20Kernel{carried}"), CancellationToken.None), 200);
        var refused = Html(await Function(Ferrum).Choose(
            Form(("name", "Abbot Commit"), ("s", "Cantor Kernel"), ("s", "Abbot Commit"), ("s", "Sister Heap")), CancellationToken.None), 422);

        foreach (var html in new[] { followed, refused })
        {
            Assert.DoesNotContain(">Abbot Commit</a>", html);
            Assert.Contains(">Cantor Kernel</a>", html);
            Assert.Contains(">Sister Heap</a>", html);
            Assert.Contains(">Brother Ferrum</a>", html);
        }
        Assert.Empty(testbed.Models.Requests);
    }

    // Taken, the Scriptorium and the Record show it; refused, the page comes back with the words and why.
    [Fact]
    public async Task TakingAName_GoesBackToTheScriptorium_WhichShowsIt()
    {
        var taken = await Function(Ferrum).Choose(Form(("name", "Brother Ferrum")), CancellationToken.None);
        Ok(await testbed.ScribeNaming().Choose(Cache, "Sister Cache", CancellationToken.None));
        var refused = await Function(Ferrum).Choose(Form(("name", "Sister Cache"), ("s", "Cantor Kernel")), CancellationToken.None);

        Assert.Equal("/scriptorium?done=named", RedirectedTo(taken));
        var scriptorium = Html(await Scriptorium(Ferrum).Page(Following(taken), CancellationToken.None), 200);
        Assert.Contains("""The Record calls you “Brother Ferrum”. <a href="/scriptorium/name">Change your name</a>""", scriptorium);
        Assert.Contains("The Record shows your name now, on every change you made.", scriptorium);
        var page = Html(refused, 422);
        Assert.Contains("""<p class="status" role="status" id="name-status">Another Scribe goes by that name. Choose another.</p>""", page);
        Assert.Contains("""value="Sister Cache" maxlength="40" aria-describedby="scribe-name-hint name-status" aria-invalid="true" """, page);
        Assert.Contains("Cantor Kernel", page);
        Assert.Empty(testbed.Models.Requests);
    }

    [Fact]
    public async Task AnEmail_ComesBack_WithWhyAndTheWords()
    {
        var page = Html(await Function(Ferrum).Choose(Form(("name", "scribe@example.com"), ("s", "Cantor Kernel")), CancellationToken.None), 422);

        Assert.Contains("never an email address", page);
        Assert.Contains("""value="scribe@example.com" maxlength="40" aria-describedby="scribe-name-hint name-status" aria-invalid="true" """, page);
        Assert.Equal(Option<string>.None, Ok(await testbed.ScribeNaming().NameOf(Ferrum, CancellationToken.None)));
    }

    [Fact]
    public async Task AnUnnamedScribe_IsAskedToChoose_OnStagingToo()
    {
        var unnamed = Html(await Scriptorium(Ferrum).Page(Get(""), CancellationToken.None), 200);
        var staging = Html(await Scriptorium(AuditEntry.StagingScribe).Page(Get(""), CancellationToken.None), 200);

        Assert.Contains("""The Record shows you as “A Scribe (…6f0d3b)”. <a href="/scriptorium/name">Choose a name</a> for it.""", unnamed);
        Assert.Contains("""The Record shows you as “A Scribe on staging”. <a href="/scriptorium/name">Choose a name</a> for it.""", staging);
    }

    [Fact]
    public async Task OnlyAScribe_MayChooseAName()
    {
        var function = new ScribeNameFunction(testbed.ScribeNaming(), testbed.NameSuggester(), new NobodyIsScribeSignIn(), NullLogger<ScribeNameFunction>.Instance);

        Assert.Equal(StatusCodes.Status403Forbidden, ((Microsoft.AspNetCore.Mvc.StatusCodeResult)await function.Page(Get(""), CancellationToken.None)).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, ((Microsoft.AspNetCore.Mvc.StatusCodeResult)await function.Choose(Form(("name", "Brother Ferrum")), CancellationToken.None)).StatusCode);
    }

    private static string Names(params string[] names) => JsonSerializer.Serialize(new { names });

    private ScribeNameFunction Function(string scribe)
        => new(testbed.ScribeNaming(), testbed.NameSuggester(), new SignedInScribe(scribe), NullLogger<ScribeNameFunction>.Instance);

    private ScriptoriumFunction Scriptorium(string scribe)
        => new(testbed.Scribes(), testbed.Amendments(), testbed.JudgingAgain(), testbed.Audit, testbed.ScribeNaming(), new SignedInScribe(scribe), testbed.Time, NullLogger<ScriptoriumFunction>.Instance);

    private AuditEntry Entry(string scribe)
        => new() { Id = Guid.NewGuid(), AtUtc = testbed.Time.GetUtcNow().UtcDateTime, Actor = scribe, Action = AuditAction.ChooseName, Title = "Sister Cache" };
}
