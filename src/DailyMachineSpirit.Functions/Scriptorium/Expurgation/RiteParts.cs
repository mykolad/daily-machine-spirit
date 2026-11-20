using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>Each part's words in a rite, its name in a form and on the page, and its field in the stored documents.</summary>
public static class RiteParts
{
    public static readonly RitePart[] All = [RitePart.Title, RitePart.Text, RitePart.HereticalTruth, RitePart.FirstFalseTruth, RitePart.SecondFalseTruth];

    /// <summary>The part's words; empty for a false truth of a rite written before them.</summary>
    public static string Of(this RitePart part, RiteWriting writing) => part switch
    {
        RitePart.Title => writing.Title,
        RitePart.Text => writing.Text,
        RitePart.HereticalTruth => writing.HereticalTruth,
        RitePart.FirstFalseTruth => writing.FalseTruths.Map(truths => truths.First).IfNone(string.Empty),
        _ => writing.FalseTruths.Map(truths => truths.Second).IfNone(string.Empty),
    };

    public static RiteWriting With(this RiteWriting writing, RitePart part, string words) => part switch
    {
        RitePart.Title => writing with { Title = words },
        RitePart.Text => writing with { Text = words },
        RitePart.HereticalTruth => writing with { HereticalTruth = words },
        RitePart.FirstFalseTruth => writing with { FalseTruths = writing.FalseTruths.Map(truths => truths with { First = words }) },
        _ => writing with { FalseTruths = writing.FalseTruths.Map(truths => truths with { Second = words }) },
    };

    /// <summary>As a form names it: <c>title</c>, <c>text</c>, <c>truth</c>, <c>false-truth-1</c>, <c>false-truth-2</c>.</summary>
    public static string Key(this RitePart part) => part switch
    {
        RitePart.Title => "title",
        RitePart.Text => "text",
        RitePart.HereticalTruth => "truth",
        RitePart.FirstFalseTruth => "false-truth-1",
        _ => "false-truth-2",
    };

    /// <summary>As the page heads it.</summary>
    public static string Label(this RitePart part) => part switch
    {
        RitePart.Title => "Title",
        RitePart.Text => "Text",
        RitePart.HereticalTruth => "Heretical Truth",
        RitePart.FirstFalseTruth => "First False Truth",
        _ => "Second False Truth",
    };

    /// <summary>As a sentence names it: "3 words of its title".</summary>
    public static string Name(this RitePart part) => part switch
    {
        RitePart.Title => "title",
        RitePart.Text => "text",
        RitePart.HereticalTruth => "Heretical Truth",
        RitePart.FirstFalseTruth => "first false truth",
        _ => "second false truth",
    };

    /// <summary>The field that holds it in a rite's or a draft's document (and a decision's).</summary>
    public static string Field(this RitePart part) => part switch
    {
        RitePart.Title => "/title",
        RitePart.Text => "/text",
        RitePart.HereticalTruth => "/hereticalTruth",
        RitePart.FirstFalseTruth => "/falseTruths/first",
        _ => "/falseTruths/second",
    };
}
