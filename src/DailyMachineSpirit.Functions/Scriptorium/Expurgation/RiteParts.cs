using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Scriptorium.Expurgation;

/// <summary>Each part's words in a rite, its name in a form and on the page, and its field in the stored documents.</summary>
public static class RiteParts
{
    public static readonly RitePart[] All = [RitePart.Title, RitePart.Text, RitePart.HereticalTruth];

    public static string Of(this RitePart part, RiteWriting writing) => part switch
    {
        RitePart.Title => writing.Title,
        RitePart.Text => writing.Text,
        _ => writing.HereticalTruth,
    };

    public static RiteWriting With(this RiteWriting writing, RitePart part, string words) => part switch
    {
        RitePart.Title => writing with { Title = words },
        RitePart.Text => writing with { Text = words },
        _ => writing with { HereticalTruth = words },
    };

    /// <summary>As a form names it: <c>title</c>, <c>text</c>, <c>truth</c>.</summary>
    public static string Key(this RitePart part) => part switch
    {
        RitePart.Title => "title",
        RitePart.Text => "text",
        _ => "truth",
    };

    /// <summary>As the page heads it.</summary>
    public static string Label(this RitePart part) => part switch
    {
        RitePart.Title => "Title",
        RitePart.Text => "Text",
        _ => "Heretical Truth",
    };

    /// <summary>As a sentence names it: "3 words of its title".</summary>
    public static string Name(this RitePart part) => part switch
    {
        RitePart.Title => "title",
        RitePart.Text => "text",
        _ => "Heretical Truth",
    };

    /// <summary>The field that holds it in a rite's or a draft's document (and a decision's).</summary>
    public static string Field(this RitePart part) => part switch
    {
        RitePart.Title => "/title",
        RitePart.Text => "/text",
        _ => "/hereticalTruth",
    };
}
