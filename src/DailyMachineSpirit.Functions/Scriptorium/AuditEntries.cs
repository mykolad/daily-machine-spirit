using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>The record's entries (#9) for what the Scribes do, each saved with the change it records.</summary>
public static class AuditEntries
{
    /// <param name="scribe">Who did it: their id, as <see cref="SignIn.IScribeSignIn"/> gives it.</param>
    /// <param name="draft">The draft it was about, if any.</param>
    /// <param name="detail">What changed, where it says more than the action.</param>
    public static AuditEntry Of(TimeProvider time, string scribe, AuditAction action, Option<Draft> draft, Option<string> detail) => new()
    {
        Id = Guid.NewGuid(),
        AtUtc = time.GetUtcNow().UtcDateTime,
        Actor = scribe,
        Action = action,
        DraftId = draft.Map(found => found.Id),
        Title = draft.Map(found => found.Writing.Title).IfNone(string.Empty),
        Detail = detail,
    };
}
