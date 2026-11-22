using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Data.Entities;

/// <summary>
/// A signed-in reader's say on a rite: their verdict, if any, and whether they confessed to having done it (#200). The
/// two are apart: one may bless a rite and confess to it.
/// </summary>
public sealed record ReaderVote(Option<Reaction> Reaction, bool Confessed)
{
    /// <summary>A reader who hasn't voted on the rite or confessed to it: nothing is kept for them.</summary>
    public static ReaderVote Nothing { get; } = new(None, false);

    public bool IsNothing => Reaction.IsNone && !Confessed;
}
