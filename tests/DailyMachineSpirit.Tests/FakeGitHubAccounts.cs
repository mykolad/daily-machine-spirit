using DailyMachineSpirit.Functions.Voters;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Tests;

/// <summary>GitHub's side of a sign-in, answering every code the same way. Records the codes.</summary>
public sealed class FakeGitHubAccounts : IGitHubAccounts
{
    private readonly Either<Error, long> answer;

    public FakeGitHubAccounts(Either<Error, long> answer)
    {
        this.answer = answer;
    }

    public List<string> Codes { get; } = [];

    public Task<Either<Error, long>> AccountFor(string code, CancellationToken cancellationToken)
    {
        Codes.Add(code);
        return Task.FromResult(answer);
    }
}
