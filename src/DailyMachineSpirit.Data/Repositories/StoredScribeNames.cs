using DailyMachineSpirit.Data.Entities;
using LanguageExt;

namespace DailyMachineSpirit.Data.Repositories;

/// <summary>The Scribes' names as read: <see cref="ETag"/> lets saving them detect a change made meanwhile; none before the first.</summary>
public sealed record StoredScribeNames(List<ScribeName> Names, Option<string> ETag);
