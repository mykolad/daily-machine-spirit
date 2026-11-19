namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// The body of <c>POST /api/witnessed</c>: the numbers of rites the reader has witnessed, to add to their list. Required,
/// so a body without it (<c>{}</c>) is refused; the function checks the numbers.
/// </summary>
public sealed record WitnessedRequest
{
    public required List<int>? Rites { get; init; }
}
