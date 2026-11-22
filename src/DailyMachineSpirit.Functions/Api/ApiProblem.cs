namespace DailyMachineSpirit.Functions.Api;

/// <summary>
/// An error of the rites API, as Problem Details (RFC 9457, <c>application/problem+json</c>, #177): its status again, a
/// short title for the kind of error and a detail for this one, so a client can say what went wrong. The type is
/// <c>about:blank</c>: the status says the kind, and there's no page of our own for each.
/// </summary>
public sealed record ApiProblem(string Type, string Title, int Status, string Detail);
