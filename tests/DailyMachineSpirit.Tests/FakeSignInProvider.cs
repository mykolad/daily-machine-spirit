using DailyMachineSpirit.Functions.Voters;
using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Tests;

/// <summary>A sign-in provider answering every code the same way. Records the codes.</summary>
public sealed class FakeSignInProvider : ISignInProvider
{
    private readonly Either<Error, string> answer;

    public FakeSignInProvider(string name, bool isConfigured, Either<Error, string> answer)
    {
        Name = name;
        IsConfigured = isConfigured;
        this.answer = answer;
    }

    public string Name { get; }

    public bool IsConfigured { get; }

    public List<string> Codes { get; } = [];

    public string AuthorizeUrl(string state) => $"https://{Name}.example/authorize?state={state}";

    public Task<Either<Error, string>> AccountFor(string code, CancellationToken cancellationToken)
    {
        Codes.Add(code);
        return Task.FromResult(answer);
    }
}
