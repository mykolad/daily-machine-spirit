using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static DailyMachineSpirit.Functions.Scriptorium.ScriptoriumHttp;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Naming;

/// <summary>
/// <c>/scriptorium/name</c> (#84): where a Scribe chooses the name the Record shows them by. The page offers names the
/// Machine Spirit suggests, which a Scribe can take as they are or change in the field first. Plain links and a form,
/// so it works without a script: a suggestion's link puts it in the field, carrying the suggestions along so the model
/// isn't asked again, and Suggest Others asks it afresh.
/// </summary>
public sealed class ScribeNameFunction
{
    private readonly ScribeNaming naming;
    private readonly NameSuggester suggester;
    private readonly ScriptoriumGate gate;
    private readonly ILogger<ScribeNameFunction> logger;

    public ScribeNameFunction(
        ScribeNaming naming, NameSuggester suggester, IScribeSignIn signIn, IOptions<ScriptoriumOptions> options, ILogger<ScribeNameFunction> logger)
    {
        this.naming = naming;
        this.suggester = suggester;
        gate = new ScriptoriumGate(signIn, options);
        this.logger = logger;
    }

    [Function("ScriptoriumName")]
    public async Task<IActionResult> Page(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/name")] HttpRequest request, CancellationToken cancellationToken)
    {
        var looking = await gate.LookingScribe(request, cancellationToken);
        if (looking.Case is IActionResult refused)
        {
            return refused;
        }

        var typed = Optional(request.Query["name"].ToString()).Filter(name => name.Length > 0);
        return await Render(request, (string)looking.Case, Carried(request.Query["s"]), typed, None, StatusCodes.Status200OK, cancellationToken);
    }

    [Function("ScriptoriumNameChoose")]
    public async Task<IActionResult> Choose(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/name")] HttpRequest request, CancellationToken cancellationToken)
    {
        var acting = await gate.ActingScribe(request, cancellationToken);
        if (acting.Case is IActionResult refused)
        {
            return refused;
        }
        if ((await ReadForm(request, cancellationToken)).Case is not IFormCollection fields)
        {
            return new BadRequestResult();
        }

        var scribe = (string)acting.Case;
        var typed = fields["name"].ToString();
        return await (await naming.Choose(scribe, typed, cancellationToken)).Match(
            Right: _ => Task.FromResult<IActionResult>(Redirect("/scriptorium?done=named")),
            Left: error =>
            {
                if (error.Code == ScribeNaming.Refused || error == Data.Repositories.ScriptoriumRepository.ChangedMeanwhile)
                {
                    var why = error.Code == ScribeNaming.Refused ? error.Message : "Another Scribe chose a name just then. Try again.";
                    return Render(request, scribe, Carried(fields["s"]), Some(typed), Some(why), StatusCodes.Status422UnprocessableEntity, cancellationToken);
                }
                return Task.FromResult(Failed(request, error));
            });
    }

    // The suggestions carried by a suggestion's link or the form, so they stay the same; none, and the model is asked.
    private static List<string> Carried(Microsoft.Extensions.Primitives.StringValues suggestions)
        => suggestions.Select(name => ScribeNaming.Acceptable(name ?? string.Empty)).Rights().Take(NameSuggester.Count).ToList();

    private async Task<IActionResult> Render(
        HttpRequest request, string scribe, List<string> carried, Option<string> typed, Option<string> message, int status, CancellationToken cancellationToken)
    {
        var all = await naming.All(cancellationToken);
        if (all.Case is Error error)
        {
            return Failed(request, error);
        }

        var names = (List<ScribeName>)all.Case;
        var chosen = names.Where(name => name.AccessId == scribe).Select(name => name.Name).HeadOrNone();
        var suggestions = carried.Count > 0
            ? carried
            : await suggester.Suggest(names.Where(name => name.AccessId != scribe).Select(name => name.Name).ToList(), cancellationToken);
        var yours = new NameInTheRecord(chosen.IfNone(() => ScribeNames.Unnamed(scribe)), chosen.IsSome);
        var inTheField = typed.IfNone(() => chosen.IfNone(() => suggestions.HeadOrNone().IfNone(string.Empty)));
        return Html(request, ScriptoriumPage.RenderName(Some(yours), suggestions, inTheField, message), status);
    }

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The name page failed: {Reason}", error.Message);
        return Html(request, ScriptoriumPage.RenderError(), StatusCodes.Status500InternalServerError);
    }
}
