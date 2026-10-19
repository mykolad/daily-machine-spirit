using DailyMachineSpirit.Data.Repositories;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using DailyMachineSpirit.Functions.Telemetry;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static DailyMachineSpirit.Functions.Scriptorium.ScriptoriumHttp;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>
/// <c>/scriptorium/expurgate</c>: the Expurgator's pages. Choosing words and seeing the rite without them are plain
/// GETs that change nothing; erasing is the one POST, which redirects back to the rite as it reads now. Every page is
/// the Expurgators' alone (<see cref="ExpurgatorSignIn"/>), behind their own Access application.
/// </summary>
public sealed class ExpurgationFunction
{
    private const int RitesListed = 30;

    private readonly Expurgations expurgations;
    private readonly ScriptoriumGate gate;
    private readonly ILogger<ExpurgationFunction> logger;

    public ExpurgationFunction(Expurgations expurgations, ExpurgatorSignIn expurgators, ILogger<ExpurgationFunction> logger)
    {
        this.expurgations = expurgations;
        gate = new ScriptoriumGate(expurgators.SignIn);
        this.logger = logger;
    }

    /// <summary>The newest rites to choose from; <c>?number=</c> opens any rite by its number.</summary>
    [Function("ScriptoriumExpurgations")]
    public async Task<IActionResult> Desk(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/expurgate")] HttpRequest request, CancellationToken cancellationToken)
    {
        if ((await gate.Refusal(request, cancellationToken)).Case is IActionResult refused)
        {
            return refused;
        }
        if (int.TryParse(request.Query["number"], out var number) && number > 0)
        {
            return Redirect($"/scriptorium/expurgate/{number}");
        }

        var message = Outcome(request).Map(done => done == "no-rite" ? "No rite has that number." : string.Empty).Filter(text => text.Length > 0);
        return (await expurgations.Newest(RitesListed, cancellationToken)).Match(
            Right: newest => Html(request, ScriptoriumPage.RenderExpurgations(newest, message), StatusCodes.Status200OK),
            Left: error => Failed(request, error));
    }

    /// <summary>
    /// A rite with each word to choose; <c>?w=</c> checks the words chosen before, if <c>?version=</c> says they were
    /// chosen in the words it has now.
    /// </summary>
    [Function("ScriptoriumExpurgate")]
    public async Task<IActionResult> Choose(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/expurgate/{number:int}")] HttpRequest request,
        int number,
        CancellationToken cancellationToken)
    {
        if ((await gate.Refusal(request, cancellationToken)).Case is IActionResult refused)
        {
            return refused;
        }

        var chosen = request.Query["w"].Select(word => word ?? string.Empty).ToHashSet();
        var chosenIn = request.Query["version"].ToString();
        return (await expurgations.Find(number, cancellationToken)).Match(
            Right: found => found.Match(
                Some: rite =>
                {
                    // Places chosen in words another erasure changed since may now be other words: chosen again, afresh.
                    var stale = chosen.Count > 0 && chosenIn != rite.Version;
                    return Html(request,
                        ScriptoriumPage.RenderExpurgate(rite, stale ? [] : chosen, stale ? Said("changed") : Outcome(request).Bind(Said)),
                        StatusCodes.Status200OK);
                },
                None: () => Redirect("/scriptorium/expurgate?done=no-rite")),
            Left: error => Failed(request, error));
    }

    /// <summary>The rite without the chosen words, each now an emoji, to erase them or choose again. Changes nothing.</summary>
    [Function("ScriptoriumExpurgateConfirm")]
    public async Task<IActionResult> Confirm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/expurgate/{number:int}/confirm")] HttpRequest request,
        int number,
        CancellationToken cancellationToken)
    {
        if ((await gate.Refusal(request, cancellationToken)).Case is IActionResult refused)
        {
            return refused;
        }

        var chosen = request.Query["w"]
            .Select(word => RedactedWord.ParseChosen(word ?? string.Empty))
            .Somes()
            .Distinct()
            .Select(word => new RedactedWord(word.Part, word.Word, LostToTime.Pick(Random.Shared)))
            .ToList();
        if (chosen.Count == 0)
        {
            return Redirect($"/scriptorium/expurgate/{number}?done=none");
        }
        return (await expurgations.Find(number, cancellationToken)).Match(
            Right: found => found.Match(
                // Places chosen in words that changed since (another erasure) may now be other words.
                Some: rite => request.Query["version"].ToString() != rite.Version
                    ? Redirect($"/scriptorium/expurgate/{number}?done=changed")
                    : Expurgations.Redacted(rite.Rite.Writing, chosen).Match(
                    Right: redacted => Html(request, ScriptoriumPage.RenderExpurgateConfirm(rite, chosen, redacted), StatusCodes.Status200OK),
                    // A place the rite's words don't have: chosen on a page shown before another erasure.
                    Left: _ => Redirect($"/scriptorium/expurgate/{number}?done=changed")),
                None: () => Redirect("/scriptorium/expurgate?done=no-rite")),
            Left: error => Failed(request, error));
    }

    [Function("ScriptoriumExpurgateErase")]
    public async Task<IActionResult> Erase(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/expurgate/{number:int}")] HttpRequest request,
        int number,
        CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(refused),
            Right: async expurgator => await (await ReadForm(request, cancellationToken)).Match(
                Some: async fields =>
                {
                    var chosen = fields["r"].Select(word => RedactedWord.Parse(word ?? string.Empty)).ToList();
                    // Every word must be one the confirming page carried: a malformed one is no choice to guess at.
                    if (chosen.Exists(word => word.IsNone))
                    {
                        return new BadRequestResult();
                    }
                    var erased = await expurgations.Erase(number, fields["version"].ToString(), chosen.Somes().ToList(), expurgator, cancellationToken);
                    return Erased(request, number, erased);
                },
                None: () => Task.FromResult<IActionResult>(new BadRequestResult())));

    private IActionResult Erased(HttpRequest request, int number, Either<Error, Option<Data.Entities.Rite>> erased)
        => erased.Match(
            Right: rite => rite.Match(
                Some: _ =>
                {
                    logger.CountScribeAction("expurgate");
                    return Redirect($"/scriptorium/expurgate/{number}?done=erased");
                },
                None: () => Redirect("/scriptorium/expurgate?done=no-rite")),
            Left: error =>
            {
                if (error == Expurgations.WordsChanged || error == Expurgations.NoSuchWord || error == ScriptoriumRepository.ChangedMeanwhile)
                {
                    return Redirect($"/scriptorium/expurgate/{number}?done=changed");
                }
                if (error == Expurgations.NothingChosen)
                {
                    return Redirect($"/scriptorium/expurgate/{number}?done=none");
                }
                return Failed(request, error);
            });

    private static Option<string> Outcome(HttpRequest request) => Optional(request.Query["done"].ToString()).Filter(done => done.Length > 0);

    private static Option<string> Said(string outcome) => outcome switch
    {
        "erased" => "Erased. The rite reads as below everywhere now, and the Record says you erased its words, not what they were.",
        "changed" => "The rite's words changed since you chose them: another Expurgator erased some. Choose again.",
        "none" => "Choose at least one word to erase.",
        _ => None,
    };

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The Expurgator's page failed: {Reason}", error.Message);
        return Html(request, ScriptoriumPage.RenderError(), StatusCodes.Status500InternalServerError);
    }
}
