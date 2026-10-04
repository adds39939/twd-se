using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class ResponsiveLayoutTests
{
    private static readonly BrowserNewPageOptions Phone = new()
    {
        ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        IsMobile = true,
        HasTouch = true,
    };

    private static readonly BrowserNewPageOptions SmallPhone = new()
    {
        ViewportSize = new ViewportSize { Width = 320, Height = 568 },
        IsMobile = true,
        HasTouch = true,
    };

    private static readonly string[] Tabs = ["decisions", "resume", "inventory", "properties"];

    private readonly PlaywrightFixture _fixture;

    public ResponsiveLayoutTests(PlaywrightFixture fixture) => _fixture = fixture;

    private static ILocator SaveItem(IPage page, string fileName) =>
        page.Locator(".save-item").Filter(new LocatorFilterOptions { HasText = fileName });

    private async Task<IPage> Open(BrowserNewPageOptions? options, params (string Season, string FileName)[] saves)
    {
        var page = await _fixture.NewPage(options);
        var directory = new Dictionary<string, string>();
        foreach (var (season, fileName) in saves)
        {
            await FakeSaveDirectory.AddSaveAsync(directory, season, fileName);
        }

        await FakeSaveDirectory.InstallAsync(page, directory);
        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(saves.Length);
        return page;
    }

    private static Task<bool> ScrollsSideways(IPage page) =>
        page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

    private static Task<double> FooterGap(IPage page) =>
        page.EvaluateAsync<double>("() => Math.abs(window.innerHeight - document.querySelector('.app-footer').getBoundingClientRect().bottom)");

    private static Task ScrollTo(IPage page, string position) =>
        page.EvaluateAsync($"() => window.scrollTo(0, {position})");

    [Fact]
    public async Task Phone_ShowsTheSaveListOrTheEditor()
    {
        var page = await Open(Phone, ("S1", "wd1_saveslot2.bundle"), ("S2", "wd2_saveslot1.bundle"));
        var saveList = page.Locator(".sidebar");
        var tabs = page.Locator(".tab-bar");
        var back = page.Locator("[data-testid='back-to-saves']");

        await SaveItem(page, "wd2_saveslot1.bundle").ClickAsync();
        await Assertions.Expect(tabs).ToBeVisibleAsync();
        await Assertions.Expect(saveList).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("[data-testid='save-bar-name']")).ToHaveTextAsync("wd2_saveslot1.bundle");

        await back.ClickAsync();
        await Assertions.Expect(saveList).ToBeVisibleAsync();
        await Assertions.Expect(tabs).ToBeHiddenAsync();
        await Assertions.Expect(SaveItem(page, "wd2_saveslot1.bundle")).ToHaveClassAsync(new Regex("selected"));

        await SaveItem(page, "wd2_saveslot1.bundle").ClickAsync();
        await Assertions.Expect(tabs).ToBeVisibleAsync();
        await Assertions.Expect(saveList).ToBeHiddenAsync();

        await back.ClickAsync();
        await SaveItem(page, "wd1_saveslot2.bundle").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='save-bar-name']")).ToHaveTextAsync("wd1_saveslot2.bundle");
        await Assertions.Expect(saveList).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Phone_OpensTheEditorAtTheTop()
    {
        var page = await Open(Phone,
            ("S1", "wd1_saveslot2.bundle"), ("S2", "wd2_saveslot1.bundle"), ("S3", "wd3_saveslot1.bundle"),
            ("S4", "wd4_saveslot2.bundle"), ("Michonne", "wdm_saveslot2.bundle"));

        await ScrollTo(page, "document.documentElement.scrollHeight");
        await page.WaitForFunctionAsync("() => window.scrollY > 0");
        await SaveItem(page, "wdm_saveslot2.bundle").ClickAsync();
        await Assertions.Expect(page.Locator(".tab-bar")).ToBeVisibleAsync();
        await page.WaitForFunctionAsync("() => window.scrollY === 0");

        await ScrollTo(page, "document.documentElement.scrollHeight");
        await page.WaitForFunctionAsync("() => window.scrollY > 0");
        await page.Locator("[data-testid='tab-properties']").ClickAsync();
        await page.WaitForFunctionAsync("() => window.scrollY === 0");
    }

    [Fact]
    public async Task Phone_OpensANewSaveInTheEditor()
    {
        var page = await Open(Phone, ("S2", "wd2_saveslot1.bundle"));

        await page.Locator("[data-testid='new-save-btn']").ClickAsync();
        var dialog = page.Locator("[data-testid='new-save-dialog']");
        await dialog.Locator("select").First.SelectOptionAsync("s2");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='save-bar-name']")).ToHaveTextAsync("wd2_saveslot2.bundle", new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator(".sidebar")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task SmallPhone_NeverScrollsSideways()
    {
        var page = await Open(SmallPhone, ("S2", "wd2_saveslot1.bundle"), ("S4", "wd4_saveslot2.bundle"));
        Assert.False(await ScrollsSideways(page));

        foreach (var fileName in new[] { "wd2_saveslot1.bundle", "wd4_saveslot2.bundle" })
        {
            if (await page.Locator("[data-testid='back-to-saves']").IsVisibleAsync())
            {
                await page.Locator("[data-testid='back-to-saves']").ClickAsync();
            }

            await SaveItem(page, fileName).ClickAsync();
            foreach (var tab in Tabs)
            {
                await page.Locator($"[data-testid='tab-{tab}']").ClickAsync();
                await Assertions.Expect(page.Locator($"[data-testid='tab-{tab}']")).ToHaveClassAsync("active");
                Assert.False(await ScrollsSideways(page), $"{fileName} {tab} scrolls sideways");
            }
        }

        await page.Locator("[data-testid='back-to-saves']").ClickAsync();
        await page.Locator("[data-testid='new-save-btn']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='new-save-dialog']")).ToBeVisibleAsync();
        var dialog = await page.Locator("[data-testid='new-save-dialog']").BoundingBoxAsync();
        Assert.NotNull(dialog);
        Assert.InRange(dialog.X, 0, 320);
        Assert.InRange(dialog.X + dialog.Width, 0, 320);
    }

    [Fact]
    public async Task Phone_KeepsTheTabsAndFooterInView()
    {
        var page = await Open(Phone, ("S2", "wd2_saveslot1.bundle"));
        Assert.True(await FooterGap(page) < 1);

        await SaveItem(page, "wd2_saveslot1.bundle").ClickAsync();
        await Assertions.Expect(page.Locator(".tab-bar")).ToBeVisibleAsync();

        foreach (var position in new[] { "0", "document.documentElement.scrollHeight / 2", "document.documentElement.scrollHeight" })
        {
            await ScrollTo(page, position);
            Assert.True(await FooterGap(page) < 1, $"footer is off the bottom at {position}");
        }

        var header = await page.Locator(".editor-header").BoundingBoxAsync();
        Assert.NotNull(header);
        Assert.InRange(header.Y, -1, 1);
    }

    [Fact]
    public async Task SmallPhone_FooterSeparatesLinksWithoutIcons()
    {
        var page = await _fixture.NewPage(SmallPhone);
        var footer = page.Locator(".app-footer");

        await Assertions.Expect(footer.Locator("svg").First).ToBeHiddenAsync();
        await Assertions.Expect(footer.Locator(".footer-sep").First).ToBeVisibleAsync();
        var box = await footer.BoundingBoxAsync();
        Assert.NotNull(box);
        Assert.True(box.Height < 40, $"footer wraps onto more than one line ({box.Height}px)");
    }

    [Fact]
    public async Task Desktop_KeepsTheSaveListBesideTheEditor()
    {
        var page = await Open(null, ("S2", "wd2_saveslot1.bundle"));

        await SaveItem(page, "wd2_saveslot1.bundle").ClickAsync();
        await Assertions.Expect(page.Locator(".tab-bar")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".sidebar")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='back-to-saves']")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator(".footer-links svg").First).ToBeVisibleAsync();
    }
}
