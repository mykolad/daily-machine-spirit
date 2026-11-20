using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace DailyMachineSpirit.Functions.Scriptorium.Backfill;

/// <summary>A false truths answer: the page to go to, and a message for each rite or draft whose false truths to write.</summary>
public sealed class FalseTruthsResponse
{
    [HttpResult]
    public IActionResult Result { get; init; } = new NotFoundResult();

    // Empty when nothing is to be written: the binding sends no message then.
    [QueueOutput(FalseTruthsBackfill.QueueName)]
    public string[] ToWrite { get; init; } = [];
}
