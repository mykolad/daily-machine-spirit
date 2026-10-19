namespace DailyMachineSpirit.Data.Repositories;

/// <summary>
/// One stored copy of a published rite's words, as read for an expurgation: the document, its version (saving it fails
/// if it changed since), and the fields that hold the words, by their path in the document (<c>/title</c>).
/// </summary>
public sealed record WordsKept(string DocumentId, string ETag, IReadOnlyDictionary<string, string> Fields);
