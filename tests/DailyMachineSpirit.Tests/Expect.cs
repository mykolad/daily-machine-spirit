using LanguageExt;
using LanguageExt.Common;

namespace DailyMachineSpirit.Tests;

public static class Expect
{
    /// <summary>The result of a call expected to succeed; a test fails with the error otherwise.</summary>
    public static T Ok<T>(Either<Error, T> result)
        => result.Match(
            Right: value => value,
            Left: error => throw new Xunit.Sdk.XunitException($"Expected success, but: {error}"));

    /// <summary>The error of a call expected to fail; a test fails if it succeeded.</summary>
    public static Error Failed<T>(Either<Error, T> result)
        => result.Match(
            Right: value => throw new Xunit.Sdk.XunitException($"Expected an error, but got: {value}"),
            Left: error => error);
}
