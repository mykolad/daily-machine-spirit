using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Scriptorium.Expurgation;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

/// <summary>The Expurgator's pages over HTTP: choosing words, seeing the rite without them, erasing them.</summary>
public sealed partial class ExpurgationFunctionTests : IAsyncLifetime
{
    private readonly BacklogTestbed testbed = new();
    private readonly FakePageCache pages = new();

    public Task InitializeAsync() => testbed.InitializeAsync();

    public Task DisposeAsync() => testbed.DisposeAsync();

    [Fact]
    public async Task TheDesk_ListsTheNewestRites_AndOpensAnyByItsNumber()
    {
        var number = await Published("The Rite of the `git push`");

        var html = Html(await Function().Desk(Get(""), CancellationToken.None), 200);
        var opened = await Function().Desk(Get($"?number={number}"), CancellationToken.None);

        Assert.Contains($"""<a href="/scriptorium/expurgate/{number}">The Rite of the <code>git push</code></a>""", html);
        Assert.Equal($"/scriptorium/expurgate/{number}", Assert.IsType<RedirectResult>(opened).Url);
        Assert.Equal("/scriptorium/expurgate?done=no-rite", Assert.IsType<RedirectResult>(await Function().Choose(Get(""), 99, CancellationToken.None)).Url);
        Assert.Contains("No rite has that number.", Html(await Function().Desk(Get("?done=no-rite"), CancellationToken.None), 200));
    }

    // Each word is a checkbox named by its part and place, so the choice works without a script; words chosen before
    // are checked again.
    [Fact]
    public async Task ARite_OffersEachWord_AndKeepsTheOnesChosenBefore()
    {
        var number = await Published("The Rite of the Cold Cache");

        var html = Html(await Function().Choose(Get($"?version={await Version(number)}&w=title:4"), number, CancellationToken.None), 200);

        Assert.Contains("""<label class="word"><input type="checkbox" name="w" value="title:4" checked>Cold</label>""", html);
        Assert.Contains("""<label class="word"><input type="checkbox" name="w" value="title:5">Cache</label>""", html);
        Assert.Contains("""<legend>Heretical Truth</legend>""", html);
        Assert.Contains($"""action="/scriptorium/expurgate/{number}/confirm">""", html);
    }

    // The rite as it will read, each chosen word an emoji, before anything changes; the form carries the emojis, so it
    // reads afterwards as it does here.
    [Fact]
    public async Task Confirming_ShowsTheRiteWithoutTheWords_AndChangesNothing()
    {
        var number = await Published("The Rite of the Cold Cache");

        var html = Html(await Function().Confirm(Get($"?version={await Version(number)}&w=title:4&w=text:3"), number, CancellationToken.None), 200);

        var title = Regex.Match(html, """<h3 id="title-preview">(.*?)</h3>""").Groups[1].Value;
        Assert.Matches("^The Rite of the .+ Cache$", title);
        Assert.DoesNotContain("Cold", title);
        Assert.Contains("Erasing 1 word of its title, 1 word of its text", html);
        Assert.Equal(2, Carried(html).Count);
        Assert.All(Carried(html), word => Assert.True(RedactedWord.Parse(word).IsSome));
        Assert.Contains("The Rite of the Cold Cache", (await Rite(number)).Writing.Title);
    }

    [Fact]
    public async Task Confirming_NoWords_OrWordsTheRiteHasnt_GoesBackToChoose()
    {
        var number = await Published("The Rite of the Cold Cache");

        var none = await Function().Confirm(Get(""), number, CancellationToken.None);
        var outside = await Function().Confirm(Get($"?version={await Version(number)}&w=title:40"), number, CancellationToken.None);

        Assert.Equal($"/scriptorium/expurgate/{number}?done=none", Assert.IsType<RedirectResult>(none).Url);
        Assert.Equal($"/scriptorium/expurgate/{number}?done=changed", Assert.IsType<RedirectResult>(outside).Url);
        Assert.Contains("Choose at least one word to erase.", Html(await Function().Choose(Get("?done=none"), number, CancellationToken.None), 200));
    }

    [Fact]
    public async Task Erasing_ChangesTheRite_AndGoesBackToIt()
    {
        var number = await Published("The Rite of the Cold Cache");
        var confirm = Html(await Function().Confirm(Get($"?version={await Version(number)}&w=title:4"), number, CancellationToken.None), 200);

        var response = await Function().Erase(Post(FormOf(confirm)), number, CancellationToken.None);

        Assert.Equal($"/scriptorium/expurgate/{number}?done=erased", Assert.IsType<RedirectResult>(response).Url);
        Assert.DoesNotContain("Cold", (await Rite(number)).Writing.Title);
        var html = Html(await Function().Choose(Get("?done=erased"), number, CancellationToken.None), 200);
        Assert.Contains("Erased. The rite reads as below everywhere now", html);
    }

    // Two Expurgators at once: the second chose places in words that have changed, so it chooses again.
    [Fact]
    public async Task Erasing_WordsThatChangedSince_GoesBackToChooseAgain()
    {
        var number = await Published("The Rite of the Cold Cache");
        var first = Html(await Function().Confirm(Get($"?version={await Version(number)}&w=title:4"), number, CancellationToken.None), 200);
        var second = Html(await Function().Confirm(Get($"?version={await Version(number)}&w=title:5"), number, CancellationToken.None), 200);
        await Function().Erase(Post(FormOf(first)), number, CancellationToken.None);

        var response = await Function().Erase(Post(FormOf(second)), number, CancellationToken.None);

        Assert.Equal($"/scriptorium/expurgate/{number}?done=changed", Assert.IsType<RedirectResult>(response).Url);
        Assert.EndsWith("Cache", (await Rite(number)).Writing.Title);
        Assert.Contains("Choose again.", Html(await Function().Choose(Get("?done=changed"), number, CancellationToken.None), 200));
    }

    // Places chosen before another erasure changed the words may now be other words: chosen again, from scratch.
    [Fact]
    public async Task ChoosingInWordsThatChangedSince_StartsAgain()
    {
        var number = await Published("The Rite of the Cold Cache");
        var chosenIn = await Version(number);
        var erasing = Html(await Function().Confirm(Get($"?version={chosenIn}&w=title:1"), number, CancellationToken.None), 200);
        await Function().Erase(Post(FormOf(erasing)), number, CancellationToken.None);

        var confirm = await Function().Confirm(Get($"?version={chosenIn}&w=title:4"), number, CancellationToken.None);
        var again = Html(await Function().Choose(Get($"?version={chosenIn}&w=title:4"), number, CancellationToken.None), 200);

        Assert.Equal($"/scriptorium/expurgate/{number}?done=changed", Assert.IsType<RedirectResult>(confirm).Url);
        Assert.DoesNotContain(" checked>", again);
        Assert.Contains("Choose again.", again);
        Assert.Contains("Cold", (await Rite(number)).Writing.Title);
    }

    // Only the site's own emojis may replace a word: anything else in the form is no choice to guess at.
    [Fact]
    public async Task Erasing_WithAWordTheFormDidntCarry_IsRefused()
    {
        var number = await Published("The Rite of the Cold Cache");
        var version = Ok(await Expurgations().Find(number, CancellationToken.None)).Map(rite => rite.Version).IfNone(string.Empty);

        var response = await Function().Erase(Post($"version={version}&r={Uri.EscapeDataString("title:4:Warm")}"), number, CancellationToken.None);

        Assert.IsType<BadRequestResult>(response);
        Assert.Contains("Cold", (await Rite(number)).Writing.Title);
    }

    [Fact]
    public async Task SomeoneWhoIsNotAnExpurgator_IsForbidden_Everywhere()
    {
        var number = await Published("The Rite of the Cold Cache");
        var function = new ExpurgationFunction(Expurgations(), new ExpurgatorSignIn(new NobodyIsScribeSignIn()), NullLogger<ExpurgationFunction>.Instance);

        IActionResult[] answers =
        [
            await function.Desk(Get(""), CancellationToken.None),
            await function.Choose(Get(""), number, CancellationToken.None),
            await function.Confirm(Get("?w=title:4"), number, CancellationToken.None),
            await function.Erase(Post($"version=x&r={Uri.EscapeDataString("title:4:💀")}"), number, CancellationToken.None),
        ];

        Assert.All(answers, answer => Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(answer).StatusCode));
        Assert.Contains("Cold", (await Rite(number)).Writing.Title);
    }

    private async Task<int> Published(string title)
    {
        await testbed.AddDraft(title, 0.9f);
        return Ok(await testbed.Publisher().PublishToday(CancellationToken.None)).Rite.Number;
    }

    private async Task<Data.Entities.Rite> Rite(int number)
        => Ok(await Expurgations().Find(number, CancellationToken.None)).IfNone(() => throw new Xunit.Sdk.XunitException("No such rite.")).Rite;

    private async Task<string> Version(int number)
        => Ok(await Expurgations().Find(number, CancellationToken.None)).Map(rite => rite.Version).IfNone(string.Empty);

    private Expurgations Expurgations() => testbed.Expurgations(testbed.Expurgation, pages);

    private ExpurgationFunction Function()
        => new(Expurgations(), new ExpurgatorSignIn(new EveryoneIsScribeSignIn()), NullLogger<ExpurgationFunction>.Instance);

    // The confirming form's hidden fields, as the browser would post them.
    private static string FormOf(string confirm)
        => string.Join("&", HiddenField().Matches(confirm).Select(field =>
            $"{field.Groups[1].Value}={Uri.EscapeDataString(WebUtility.HtmlDecode(field.Groups[2].Value))}"));

    private static List<string> Carried(string confirm)
        => HiddenField().Matches(confirm).Where(field => field.Groups[1].Value == "r").Select(field => WebUtility.HtmlDecode(field.Groups[2].Value)).ToList();

    private static HttpRequest Get(string query)
    {
        var request = new DefaultHttpContext().Request;
        request.Method = "GET";
        if (query.Length > 0)
        {
            request.QueryString = new QueryString(query);
        }
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

    private static string Html(IActionResult result, int status)
    {
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(status, content.StatusCode);
        return content.Content ?? string.Empty;
    }

    [GeneratedRegex("""<input type="hidden" name="(\w+)" value="([^"]*)">""")]
    private static partial Regex HiddenField();
}
