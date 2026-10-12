using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Voters;

/// <summary>GitHub's side of a sign-in.</summary>
public interface IGitHubAccounts
{
    /// <summary>
    /// The id of the account that signed in, for the code GitHub sent back with the reader. The token the code is
    /// exchanged for only reads that id: it's revoked at once, never kept.
    /// </summary>
    Task<Either<Error, long>> AccountFor(string code, CancellationToken cancellationToken);
}
