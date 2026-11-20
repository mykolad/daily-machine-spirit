using LanguageExt;
using Microsoft.Playwright;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.BrowserTests;

/// <summary>
/// One headless Chromium for the test class, each test in a fresh context of its own (no cookies, nothing in
/// <c>localStorage</c>). It needs Playwright's browser installed: <c>pwsh bin/Debug/net10.0/playwright.ps1 install
/// chromium</c> after a build (CI installs it with the system libraries it needs).
/// </summary>
public sealed class Chromium : IAsyncLifetime
{
    private Option<(IPlaywright Playwright, IBrowser Browser)> launched = None;

    public IBrowser Browser => launched
        .Map(running => running.Browser)
        .IfNone(() => throw new InvalidOperationException("The browser is launched in InitializeAsync."));

    public async ValueTask InitializeAsync()
    {
        var playwright = await Playwright.CreateAsync();
        launched = (playwright, await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var running in launched)
        {
            await running.Browser.DisposeAsync();
            running.Playwright.Dispose();
        }
    }
}
