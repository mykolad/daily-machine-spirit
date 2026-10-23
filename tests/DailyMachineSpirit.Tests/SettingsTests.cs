using DailyMachineSpirit.Functions.Generation;

namespace DailyMachineSpirit.Tests;

/// <summary>The settings the app ships with (<c>machine-spirit.jsonc</c>): all there, and within what Cloudflare allows.</summary>
public class SettingsTests
{
    // Cloudflare gives up on a request after this long, so a page the Scribe waits for must come back sooner.
    private static readonly TimeSpan CloudflareWaits = TimeSpan.FromSeconds(100);

    // A rewrite asks each model while the Scribe waits; a distillation asks each model, then checks what it distilled.
    [Fact]
    public void WhatAScribeWaitsFor_FitsInCloudflaresTime()
    {
        var writing = Settings.RiteWriting;
        var distilling = Settings.PreceptDistilling;

        var rewrite = Longest(writing.PatienceWhileTheScribeWaits, writing.Models.Length);
        var distillation = Longest(distilling.Patience, distilling.Models.Length)
            + Settings.PreceptCheck.Patience.OverallLimit.IfNone(() => throw new Xunit.Sdk.XunitException("The precept check has no limit."));
        var names = Longest(Settings.ScribeNameSuggestions.Patience, 1);
        // A saved amendment is judged again while the Scribe waits: Jev and every score at once.
        var judging = new[] { Longest(Settings.Jev.Patience, 1), Longest(Settings.QualityJudging.Patience, 1) }.Max();

        Assert.All([rewrite, distillation, names, judging], waited => Assert.True(waited < CloudflareWaits, $"{waited} is past {CloudflareWaits}."));
    }

    // The binder leaves a value unset when its key is missing: every number, name and list must come from the file.
    [Fact]
    public void EverySetting_IsInTheFile()
    {
        object[] sections =
        [
            Settings.RiteWriting, Settings.Backlog, Settings.DuplicateRiteCheck, Settings.AilmentWriting, Settings.PreceptDistilling, Settings.PreceptCheck,
            Settings.ScribeNameSuggestions, Settings.Augury, Settings.QualityJudging, Settings.Jev, Settings.PagePurging, Settings.PublicPages, Settings.ScriptoriumPages,
        ];

        var unset = sections
            .SelectMany(section => section.GetType().GetProperties().Select(property => (section, property)))
            .Where(found => found.property.Name != "ApiKey" && found.property.Name != "Enabled")
            .Where(found => found.property.GetValue(found.section) switch
            {
                int number => number <= 0,
                float number => number <= 0,
                TimeSpan time => time <= TimeSpan.Zero,
                string text => text.Length == 0,
                string[] list => list.Length == 0,
                _ => false,
            })
            .Select(found => $"{found.section.GetType().Name}.{found.property.Name}");

        Assert.Empty(unset);
    }

    // Each model call honours only some of a Patience's limits (a single try has no pause, the precept check has one
    // budget for all its models). Its part of the file sets exactly those: a limit the code ignores would quietly do
    // nothing, and one it needs, left out, would quietly be none.
    [Theory]
    [InlineData("RiteWriting:Patience", "TriesPerModel PauseBetweenTries")]
    [InlineData("RiteWriting:PatienceWhileTheScribeWaits", "TriesPerModel TimePerTry")]
    [InlineData("DuplicateRiteCheck:Patience", "TimePerTry")]
    [InlineData("AilmentWriting:Patience", "TimePerTry")]
    [InlineData("PreceptDistilling:Patience", "TriesPerModel TimePerTry")]
    [InlineData("PreceptCheck:Patience", "TimeInAll")]
    [InlineData("ScribeNameSuggestions:Patience", "TimePerTry")]
    [InlineData("Jev:Patience", "TimePerTry")]
    [InlineData("QualityJudging:Patience", "TimePerTry")]
    public void EachPatience_SetsTheLimitsItsCodeHonours(string path, string honoured)
        => Assert.Equal(honoured.Split(' ').Order(), Settings.KeysIn(path).Order());

    private static TimeSpan Longest(Patience patience, int models)
        => (patience.TryLimit.IfNone(() => throw new Xunit.Sdk.XunitException("A Scribe's wait has no time limit.")) + patience.PauseBetweenTries)
            * patience.TriesPerModel * models;
}
