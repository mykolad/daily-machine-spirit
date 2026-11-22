using DailyMachineSpirit.Functions.Generation.Feasts;
using LanguageExt;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>
/// What a new draft is asked to do differently from the rites before it (#193): take the form they used least, and rest
/// the images they lean on; and, written for a feast (#202), be about that feast.
/// </summary>
/// <param name="WornImages">The images to rest (<see cref="Writing.WornImages"/>), the most used first; empty when none is worn.</param>
/// <param name="Feast">The feast it's written for, or None for an ordinary day's.</param>
public sealed record RiteVariety(RiteForm Form, IReadOnlyList<string> WornImages, Option<Feast> Feast);
