namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// A rite's confessions in words (#200), as the rite's page and the Reliquary's rows say them: "41 have confessed",
/// "1 hath confessed", "None have confessed". The page's script words them the same, and knows too when the one is the
/// reader ("Thou alone hast confessed").
/// </summary>
public static class ConfessedWords
{
    public static string Of(int count) => count switch
    {
        0 => "None have confessed",
        1 => "1 hath confessed",
        _ => $"{Html.Count(count)} have confessed",
    };
}
