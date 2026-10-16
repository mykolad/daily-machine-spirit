using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The Precepts as read: <see cref="ETag"/> lets saving them detect a change made meanwhile; none before the first.</summary>
public sealed record StoredPrecepts(PreceptBook Book, Option<string> ETag);
