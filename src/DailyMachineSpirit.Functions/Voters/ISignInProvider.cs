using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Functions.Voters;

/// <summary>A way readers sign in to vote: GitHub for now, others alongside it later.</summary>
public interface ISignInProvider
{
    /// <summary>
    /// Its name in the sign-in's addresses (<c>/auth/github</c>) and in the voter key (<c>github:&lt;id&gt;</c>), so
    /// accounts at two providers never share a key. Fixed once readers have voted with it: a new name makes them new voters.
    /// </summary>
    string Name { get; }

    bool IsConfigured { get; }

    /// <summary>Where to send the reader to sign in, with the state that ties the answer to their browser.</summary>
    string AuthorizeUrl(string state);

    /// <summary>
    /// The id of the account that signed in, as the provider names it, for the code the provider sent back with the
    /// reader. Nothing else about the account is read, and any token it takes to read it is revoked, never kept.
    /// </summary>
    Task<Either<Error, string>> AccountFor(string code, CancellationToken cancellationToken);
}
