using System.ComponentModel;
using System.Diagnostics;
using LanguageExt.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DailyMachineSpirit.Mcp;

/// <summary>
/// The connector's tools (#119): the public rites API (docs/api.md) for an assistant that can't fetch it itself, the
/// Heretical Truths sealed as on the site. The descriptions tell the assistant what the skill tells it
/// (skills/consult-the-machine-spirit): match the trouble here, and break a seal only when the user asks.
/// </summary>
[McpServerToolType]
public sealed class RiteTools
{
    public const string ListRitesName = "list_rites";

    public const string RevealHereticalTruthName = "reveal_heretical_truth";

    public const string Unavailable = "The rites can't be read just now. Try again in a minute.";

    private readonly RitesApi api;
    private readonly ILogger<RiteTools> logger;

    public RiteTools(RitesApi api, ILogger<RiteTools> logger)
    {
        this.api = api;
        this.logger = logger;
    }

    [McpServerTool(Name = ListRitesName, Title = "List the rites", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        Every rite The Daily Machine Spirit has published, the newest first: a prayer or a ritual a tech-priest, who no
        longer understands the machine, recites for a developer's trouble. Each has its number, day, kind, title, text,
        ailment (the trouble it's for, in a line, without its fix: match a trouble on it first), subjects and page.
        Their Heretical Truths, the real fix, stay sealed. To prescribe one, quote its title, number, text and page as
        given, never words of your own, then ask whether the user dares to hear its Heretical Truth. If none fits the
        trouble, say so rather than stretch one.
        """)]
    public async Task<CallToolResult> ListRites(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        return (await api.GetRites(cancellationToken)).Match(
            Right: rites => Counted(ListRitesName, started, LogCounters.Answered, Answer(rites)),
            Left: error => Counted(ListRitesName, started, LogCounters.Unavailable, Unanswered(error)));
    }

    [McpServerTool(Name = RevealHereticalTruthName, Title = "Reveal a rite's Heretical Truth", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("""
        A rite's Heretical Truth: one or two plain sentences on what really happens, and what to do instead. Call it
        only after the user agrees to hear it, and only for the rite prescribed: the seal is the point.
        """)]
    public async Task<CallToolResult> RevealHereticalTruth(
        [Description("The rite's number, as list_rites gives it (NO. 12 is 12).")] int number,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        return (await api.GetHereticalTruth(number, cancellationToken)).Match(
            Right: found => found.Match(
                Some: truth => Counted(RevealHereticalTruthName, started, LogCounters.Answered, Answer(truth)),
                None: () => Counted(RevealHereticalTruthName, started, LogCounters.NoSuchRite,
                    Refusal($"No rite NO. {number} has been published."))),
            Left: error => Counted(RevealHereticalTruthName, started, LogCounters.Unavailable, Unanswered(error)));
    }

    private CallToolResult Counted(string tool, long started, string outcome, CallToolResult result)
    {
        logger.CountToolCall(tool, outcome, Stopwatch.GetElapsedTime(started));
        return result;
    }

    private static CallToolResult Answer(string text) => Text(text, false);

    // A failure is the tool's answer, marked as one, so the assistant can say so rather than make a rite up.
    private static CallToolResult Refusal(string text) => Text(text, true);

    private CallToolResult Unanswered(Error error)
    {
        logger.LogWarning(error.ToException(), "The rites API couldn't be read: {Reason}", error.Message);
        return Refusal(Unavailable);
    }

    private static CallToolResult Text(string text, bool isError) => new()
    {
        Content = [new TextContentBlock { Text = text }],
        IsError = isError,
    };
}
