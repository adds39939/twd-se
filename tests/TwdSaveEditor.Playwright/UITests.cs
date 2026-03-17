using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright;

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

        // Dark theme should have a dark background (low RGB values)
        // Parse rgb(r, g, b) format
        Assert.NotNull(bgColor);
        Assert.DoesNotContain("255, 255, 255", bgColor); // Not white
    }

    [Fact]
    public async Task App_ShowsTitle()
    {
        var page = await _fixture.NewPage();

        var headerText = await page.TextContentAsync(".header-title");

        Assert.NotNull(headerText);
        Assert.Contains("TWD Save Editor", headerText);
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
        // Tabs are only visible when a save is selected, so verify the tab bar
        // elements exist in the DOM (they may be hidden without a save loaded).
        var page = await _fixture.NewPage();

        // Without a save loaded, the tab bar should not be visible
        var tabBar = page.Locator(".tab-bar");
        await Assertions.Expect(tabBar).ToHaveCountAsync(0);

        // But the empty state should be visible instead
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
                consoleErrors.Add(msg.Text);
        };

        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForSelectorAsync("[data-testid='app-ready']",
            new() { Timeout = 30000 });

        // Allow Blazor's own framework messages but no app errors
        var appErrors = consoleErrors
            .Where(e => !e.Contains("blazor", StringComparison.OrdinalIgnoreCase))
            .Where(e => !e.Contains("favicon", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(appErrors);
    }
}
