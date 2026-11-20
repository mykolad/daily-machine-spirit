using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Scriptorium.SignIn;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static DailyMachineSpirit.Functions.Scriptorium.ScriptoriumHttp;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Backfill;

/// <summary>
/// <c>/scriptorium/false-truths</c>: the backfill of false truths (#199), for the Scribes. Starting it leaves a message per
/// rite or draft, which the queue writes one at a time (each a model's answer); accepting or asking again acts on one
/// rite's proposal. Each action redirects back to the page, so reloading never repeats it.
/// </summary>
public sealed class FalseTruthsFunction
{
    private const string Page = "/scriptorium/false-truths";

    private readonly FalseTruthsBackfill backfill;
    private readonly ScriptoriumGate gate;
    private readonly ILogger<FalseTruthsFunction> logger;

    public FalseTruthsFunction(FalseTruthsBackfill backfill, IScribeSignIn signIn, ILogger<FalseTruthsFunction> logger)
    {
        this.backfill = backfill;
        gate = new ScriptoriumGate(signIn);
        this.logger = logger;
    }

    [Function("ScriptoriumFalseTruths")]
    public async Task<IActionResult> View(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scriptorium/false-truths")] HttpRequest request, CancellationToken cancellationToken)
    {
        if ((await gate.Refusal(request, cancellationToken)).Case is IActionResult refused)
        {
            return refused;
        }
        return await Shown(request, Optional(request.Query["done"].ToString()).Bind(Said), None, StatusCodes.Status200OK, cancellationToken);
    }

    [Function("ScriptoriumFalseTruthsWrite")]
    public async Task<FalseTruthsResponse> Write(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/false-truths/write")] HttpRequest request, CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(new FalseTruthsResponse { Result = refused }),
            Right: async _ => (await backfill.Pending(cancellationToken)).Match(
                Right: pending => new FalseTruthsResponse { Result = Redirect($"{Page}?done=writing"), ToWrite = [.. pending] },
                Left: error => new FalseTruthsResponse { Result = Failed(request, error) }));

    [Function("ScriptoriumFalseTruthsAccept")]
    public async Task<IActionResult> Accept(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/false-truths/{number:int}/accept")] HttpRequest request,
        int number,
        CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(refused),
            Right: scribe => AcceptedBy(scribe, request, number, cancellationToken));

    [Function("ScriptoriumFalseTruthsAgain")]
    public async Task<FalseTruthsResponse> Again(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "scriptorium/false-truths/{number:int}/again")] HttpRequest request,
        int number,
        CancellationToken cancellationToken)
        => await (await gate.ActingScribe(request, cancellationToken)).Match(
            Left: refused => Task.FromResult(new FalseTruthsResponse { Result = refused }),
            Right: async _ => (await backfill.Withdraw(number, cancellationToken)).Match(
                Right: withdrawn => withdrawn
                    ? new FalseTruthsResponse { Result = Redirect($"{Page}?done=again"), ToWrite = [FalseTruthsBackfill.RiteMessage(number)] }
                    : new FalseTruthsResponse { Result = Redirect($"{Page}?done=gone") },
                Left: error => new FalseTruthsResponse { Result = Failed(request, error) }));

    // A failure is thrown, so the message comes back for another try (host.json); the ones written before it stay.
    [Function("WriteFalseTruths")]
    public async Task Run([QueueTrigger(FalseTruthsBackfill.QueueName)] string message, CancellationToken cancellationToken)
    {
        if ((await backfill.Write(message, cancellationToken)).Case is Error error)
        {
            logger.LogError(error.ToException(), "False truths couldn't be written for {Message}: {Reason}", message, error.Message);
            throw error.ToException();
        }
    }

    private async Task<IActionResult> AcceptedBy(ScribeId scribe, HttpRequest request, int number, CancellationToken cancellationToken)
    {
        if ((await ReadForm(request, cancellationToken)).Case is not IFormCollection fields)
        {
            return new BadRequestResult();
        }
        // A browser posts a textarea's line breaks as CRLF, which its maxlength counted as one character.
        var (first, second) = (fields["false-truth-1"].ToString().Replace("\r\n", "\n"), fields["false-truth-2"].ToString().Replace("\r\n", "\n"));
        return await (await backfill.Accept(number, first, second, scribe, cancellationToken)).Match(
            Right: taken => Task.FromResult(Redirect($"{Page}?done={(taken ? "accepted" : "gone")}")),
            Left: error => error is AmendmentRefused
                ? Shown(request, Some($"Not accepted: {error.Message}"), Some((number, new FalseTruths(first, second))), StatusCodes.Status422UnprocessableEntity, cancellationToken)
                : Task.FromResult(Failed(request, error)));
    }

    // A refused acceptance shows the Scribe's words where they typed them (typed), to correct rather than type again.
    private async Task<IActionResult> Shown(
        HttpRequest request, Option<string> message, Option<(int Number, FalseTruths Words)> typed, int status, CancellationToken cancellationToken)
    {
        var (pending, proposals) = (backfill.Pending(cancellationToken), backfill.Proposals(cancellationToken));
        var (without, proposed) = (await pending, await proposals);
        var read = from waiting in without
                   from shown in proposed
                   select ScriptoriumPage.RenderFalseTruths(waiting.Count, shown.Select(proposal => AsTyped(proposal, typed)).ToList(), message);
        return read.Match(Right: html => Html(request, html, status), Left: error => Failed(request, error));
    }

    private static ProposedFalseTruths AsTyped(ProposedFalseTruths proposal, Option<(int Number, FalseTruths Words)> typed)
        => typed.Filter(words => words.Number == proposal.Number).Match(Some: words => proposal with { Proposed = words.Words }, None: () => proposal);

    private static Option<string> Said(string outcome) => outcome switch
    {
        "writing" => "Asked. The false truths arrive as they're written: reload to see them.",
        "accepted" => "Accepted. The rite's page asks its readers to guess now.",
        "again" => "Asked again. The new ones arrive as they're written.",
        "gone" => "Another Scribe dealt with it meanwhile.",
        _ => None,
    };

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The false truths page failed: {Reason}", error.Message);
        return Html(request, ScriptoriumPage.RenderError(), StatusCodes.Status500InternalServerError);
    }
}
