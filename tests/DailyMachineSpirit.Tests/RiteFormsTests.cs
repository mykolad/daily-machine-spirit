using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Functions.Generation.Writing;
using LanguageExt;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public class RiteFormsTests
{
    // A rite written before forms were stored is known by its title's first words; one asked for a form keeps it,
    // whatever its title says.
    [Theory]
    [InlineData(null, "The Rite of Re-Run", "rite")]
    [InlineData(null, "Litany of the Clean Cache", "litany")]
    [InlineData(null, "The Vigil of the Green Check", "vigil")]
    [InlineData(null, "Sacred Restart", null)]
    [InlineData(null, "Prayer for the Seventh Litany", null)]
    [InlineData("penance", "The Rite of Atonement", "penance")]
    [InlineData("no-such-form", "The Litany of Retries", "litany")]
    public void Of_IsTheStoredForm_ElseTheOneTheTitleNames(string? stored, string title, string? form)
        => Assert.Equal(Optional(form), RiteForms.Of(Optional(stored), title).Map(found => found.Key));

    // The kind's form the recent rites and drafts took least; among equals, the first listed.
    [Fact]
    public void ToWrite_IsTheKindsFormTakenLeast()
    {
        var taken = new[] { "rite", "rite", "vigil", "penance", "exorcism", "catechism", "litany" }
            .Select(key => RiteForms.Find(key))
            .Somes();

        Assert.Equal("vigil", RiteForms.ToWrite(RiteKind.Ritual, taken).Key);
        Assert.Equal("canticle", RiteForms.ToWrite(RiteKind.Prayer, taken).Key);
        Assert.Equal("rite", RiteForms.ToWrite(RiteKind.Ritual, []).Key);
    }

    // Each kind has forms of its own, so taking turns in forms never fights taking turns in kinds.
    [Fact]
    public void EveryKind_HasFormsOfItsOwn()
        => Assert.All(Enum.GetValues<RiteKind>(), kind => Assert.True(RiteForms.All.Count(form => form.Kind == kind) >= 3));

    // Counted as words, plural or not, the most used first; an image in fewer texts than that isn't worn.
    [Fact]
    public void WornImages_AreTheWatchedImagesUsedOften_TheMostUsedFirst()
    {
        string[] texts =
        [
            "Pass the gate, O supplicant, and ask not why.",
            "Two gates stand open; the Seal is green.",
            "Ask not for the green mark at the gateway.",
            "Bless the seal. Ask not twice.",
        ];

        Assert.Equal(["ask not", "gate", "seal", "green"], WornImages.In(texts, 2));
        Assert.Equal(["ask not"], WornImages.In(texts, 3));
    }

    // What the writer is offered instead mustn't be worn itself.
    [Fact]
    public void FreshImages_AreNoneOfTheWatchedOnes()
        => Assert.Empty(WornImages.In(WornImages.Fresh, 1));
}
