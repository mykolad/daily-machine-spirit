using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.Generation.Writing;

/// <summary>A form a rite can take (<see cref="RiteForms"/>): what the writer is asked to write, and its title names.</summary>
/// <param name="Key">What a rite's writing stores (<see cref="RiteWriting.Form"/>).</param>
/// <param name="Name">The word a title of the form uses: "Litany of the Clean Cache".</param>
/// <param name="Shape">What makes it that form, as the writer is told.</param>
public sealed record RiteForm(string Key, string Name, RiteKind Kind, string Shape);
