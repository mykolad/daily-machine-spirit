using System.Runtime.CompilerServices;

namespace DailyMachineSpirit.SmokeTests;

/// <summary>A test that votes as the <see cref="TestVoter"/>: skipped where the workflow gives no secret to sign in with.</summary>
public sealed class TestVoterFactAttribute : FactAttribute
{
    // The compiler fills in where the test is, for xUnit's report: caller information needs the default values.
    public TestVoterFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!TestVoter.IsAvailable)
        {
            Skip = $"No {TestVoter.SecretVariable}: this environment's votes aren't for tests (production's are public).";
        }
    }
}
