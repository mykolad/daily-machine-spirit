namespace DailyMachineSpirit.SmokeTests;

/// <summary>A test that votes as the <see cref="TestVoter"/>: skipped where the workflow gives no secret to sign in with.</summary>
public sealed class TestVoterFactAttribute : FactAttribute
{
    public TestVoterFactAttribute()
    {
        if (!TestVoter.IsAvailable)
        {
            Skip = $"No {TestVoter.SecretVariable}: this environment's votes aren't for tests (production's are public).";
        }
    }
}
