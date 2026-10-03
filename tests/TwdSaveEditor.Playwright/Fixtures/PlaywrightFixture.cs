using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Fixtures;

public class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private PublishedSite _site = null!;

    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl => _site.Url;

    public async Task InitializeAsync()
    {
        Program.Main(["install", "chromium"]);

        _site = await PublishedSite.StartAsync();
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        _playwright.Dispose();
        await _site.DisposeAsync();
    }

    public async Task<IPage> NewPage()
    {
        var page = await Browser.NewPageAsync();
        await page.GotoAsync(BaseUrl);

        await page.WaitForSelectorAsync("[data-testid='app-ready']",
            new PageWaitForSelectorOptions { Timeout = 30000 });

        return page;
    }
}
