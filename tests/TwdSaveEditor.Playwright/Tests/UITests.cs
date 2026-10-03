using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class UITests
{
    private readonly PlaywrightFixture _fixture;

    public UITests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task App_LoadsWithDarkTheme()
    {
        var page = await _fixture.NewPage();

        var bgColor = await page.EvalOnSelectorAsync<string>(
            "body", "el => getComputedStyle(el).backgroundColor");

        Assert.NotNull(bgColor);
        Assert.DoesNotContain("255, 255, 255", bgColor);
    }

    [Fact]
    public async Task App_ShowsTitle()
    {
        var page = await _fixture.NewPage();

        var headerText = await page.TextContentAsync(".header-title");

        Assert.NotNull(headerText);
        Assert.Contains("Save Editor", headerText);
    }

    [Fact]
    public async Task App_ShowsDirectoryPickerButton()
    {
        var page = await _fixture.NewPage();

        var button = page.Locator("[data-testid='open-directory']");
        await Assertions.Expect(button).ToBeVisibleAsync();
    }

    [Fact]
    public async Task App_ShowsTabBar()
    {
        var page = await _fixture.NewPage();

        var tabBar = page.Locator(".tab-bar");
        await Assertions.Expect(tabBar).ToHaveCountAsync(0);

        var emptyState = page.Locator(".empty-state");
        await Assertions.Expect(emptyState).ToBeVisibleAsync();
    }

    [Fact]
    public async Task App_HasNoConsoleErrors()
    {
        var consoleErrors = new List<string>();
        var page = await _fixture.Browser.NewPageAsync();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                consoleErrors.Add(msg.Text);
            }
        };

        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForSelectorAsync("[data-testid='app-ready']",
            new() { Timeout = 30000 });

        var appErrors = consoleErrors
            .Where(e => !e.Contains("blazor", StringComparison.OrdinalIgnoreCase))
            .Where(e => !e.Contains("favicon", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(appErrors);
    }

    [Fact]
    public async Task TitleBar_ShowsTheAppIcon()
    {
        var page = await _fixture.NewPage();
        await page.WaitForSelectorAsync("[data-testid='app-ready']", new() { Timeout = 30000 });

        var icon = page.Locator("[data-testid='app-icon']");
        await Assertions.Expect(icon).ToBeVisibleAsync();
        Assert.True(await icon.EvaluateAsync<bool>("img => img.complete && img.naturalWidth > 0"));
        await Assertions.Expect(page.Locator(".header-title")).ToContainTextAsync("Save Editor");
    }

    [Fact]
    public async Task Footer_ShowsTheDevVersionInADevelopmentBuild()
    {
        var page = await _fixture.NewPage();

        await Assertions.Expect(page.Locator("[data-testid='app-version']")).ToHaveTextAsync("dev");
    }
}
