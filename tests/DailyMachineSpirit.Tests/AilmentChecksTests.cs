using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Writing;
using static DailyMachineSpirit.Tests.Expect;

namespace DailyMachineSpirit.Tests;

/// <summary>An ailment written alone, for a rite written before rites had one: the rules an ailment always follows.</summary>
public sealed class AilmentChecksTests
{
    [Fact]
    public void CheckAilment_TakesOne_Trimmed()
        => Assert.Equal("A build that passes on the second run.", Ok(RiteChecks.CheckAilment("  A build that passes on the second run. ")));

    [Theory]
    [InlineData("   ")]
    [InlineData("A chatbot that gives the wrong answer.")]
    [InlineData("The Omnissiah won't answer.")]
    [InlineData("The Imperium's builds keep failing.")]
    [InlineData("Running npm install --force never helps.")]
    public void CheckAilment_RefusesWhatAnAilmentMustNotBe(string ailment)
        => Assert.True(RiteChecks.CheckAilment(ailment).IsLeft);

    [Fact]
    public void CheckAilment_RefusesOneTooLong()
        => Assert.True(RiteChecks.CheckAilment(new string('a', RiteWriting.MaxAilmentLength + 1)).IsLeft);
}
