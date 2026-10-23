using DailyMachineSpirit.Functions.Caching;
using DailyMachineSpirit.Functions.Generation;
using DailyMachineSpirit.Functions.Generation.Judging;
using DailyMachineSpirit.Functions.Generation.Precepts;
using DailyMachineSpirit.Functions.Generation.Writing;
using DailyMachineSpirit.Functions.Pages;
using DailyMachineSpirit.Functions.Scriptorium;
using DailyMachineSpirit.Functions.Scriptorium.Naming;
using Microsoft.Extensions.Configuration;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// The app's own <c>machine-spirit.jsonc</c>, read as the app reads it: the tests run with its values, changing only what
/// they must (with <c>with</c>), so they check the settings the app ships with.
/// </summary>
public static class Settings
{
    private static readonly IConfiguration File = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "machine-spirit.jsonc"), optional: false)
        .Build();

    public static RiteWritingOptions RiteWriting => Section<RiteWritingOptions>(RiteWritingOptions.SectionName);

    public static BacklogOptions Backlog => Section<BacklogOptions>(BacklogOptions.SectionName);

    public static DuplicateRiteCheckOptions DuplicateRiteCheck => Section<DuplicateRiteCheckOptions>(DuplicateRiteCheckOptions.SectionName);

    public static PreceptDistillingOptions PreceptDistilling => Section<PreceptDistillingOptions>(PreceptDistillingOptions.SectionName);

    public static PreceptCheckOptions PreceptCheck => Section<PreceptCheckOptions>(PreceptCheckOptions.SectionName);

    public static ScribeNameSuggestionsOptions ScribeNameSuggestions => Section<ScribeNameSuggestionsOptions>(ScribeNameSuggestionsOptions.SectionName);

    public static AuguryOptions Augury => Section<AuguryOptions>(AuguryOptions.SectionName);

    public static JevOptions Jev => Section<JevOptions>(JevOptions.SectionName);

    public static QualityJudgingOptions QualityJudging => Section<QualityJudgingOptions>(QualityJudgingOptions.SectionName);

    public static PagePurgingOptions PagePurging => Section<PagePurgingOptions>(PagePurgingOptions.SectionName);

    public static PublicPagesOptions PublicPages => Section<PublicPagesOptions>(PublicPagesOptions.SectionName);

    public static ScriptoriumPagesOptions ScriptoriumPages => Section<ScriptoriumPagesOptions>(ScriptoriumPagesOptions.SectionName);

    /// <summary>The keys a part of the file sets, such as a section's <c>Patience</c>.</summary>
    public static IReadOnlyList<string> KeysIn(string path) => File.GetSection(path).GetChildren().Select(key => key.Key).ToList();

    private static T Section<T>(string name) where T : new()
        => File.GetSection(name).Get<T>(binder => binder.ErrorOnUnknownConfiguration = true) ?? new T();
}
