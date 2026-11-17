namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// What a new draft is asked to do differently from the rites before it (#193): take the form they used least, and rest
/// the images they lean on.
/// </summary>
/// <param name="WornImages">The images to rest (<see cref="Writing.WornImages"/>), the most used first; empty when none is worn.</param>
public sealed record RiteVariety(RiteForm Form, IReadOnlyList<string> WornImages);
