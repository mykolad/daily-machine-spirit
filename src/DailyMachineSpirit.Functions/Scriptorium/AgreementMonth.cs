using LanguageExt;

namespace DailyMachineSpirit.Functions.Scriptorium;

/// <summary>
/// How far the Scribes agreed with the Augury in one month (#12): how many of its top picks they burned, and how far
/// they moved the drafts they moved from where it had placed them. Only drafts the Augury judged count against it.
/// </summary>
public sealed record AgreementMonth(
    DateOnly FirstDayUtc,
    int ScribeDecisions,
    int DraftsBurned,
    int AuguryTopPicksBurned,
    int JudgedDraftsMoved,
    Option<double> AveragePlacesFromAuguryPlace);
