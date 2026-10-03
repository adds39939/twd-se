using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class UnsavedChangesTests
{
    private const string LeavingIsBlocked = """
        (expected) => {
            const e = new Event('beforeunload', { cancelable: true });
            window.dispatchEvent(e);
            return e.defaultPrevented === expected;
        }
        """;

    private readonly PlaywrightFixture _fixture;

    public UnsavedChangesTests(PlaywrightFixture fixture) => _fixture = fixture;

    private static ILocator SaveItem(IPage page, string fileName) =>
        page.Locator(".save-item").Filter(new LocatorFilterOptions { HasText = fileName });

    private static Task ExpectLeavingBlocked(IPage page, bool blocked) =>
        page.WaitForFunctionAsync(LeavingIsBlocked, blocked);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(50);
        }

        Assert.True(condition());
    }

    private async Task<IPage> OpenTwoSaves()
    {
        var page = await _fixture.NewPage();
        var directory = new Dictionary<string, string>();
        await FakeSaveDirectory.AddSaveAsync(directory, "S2", "wd2_saveslot1.bundle");
        await FakeSaveDirectory.AddSaveAsync(directory, "Michonne", "wdm_saveslot2.bundle");
        await FakeSaveDirectory.InstallAsync(page, directory);

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(2);
        return page;
    }

    private static async Task RestartFromEpisode2(IPage page, string fileName)
    {
        await SaveItem(page, fileName).ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("2");
    }

    [Fact]
    public async Task UnsavedChanges_StayWithTheSaveTheyWereMadeIn()
    {
        var page = await OpenTwoSaves();
        await ExpectLeavingBlocked(page, false);

        await RestartFromEpisode2(page, "wd2_saveslot1.bundle");
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");
        await Assertions.Expect(SaveItem(page, "wd2_saveslot1.bundle").Locator("[data-testid='save-unsaved']")).ToBeVisibleAsync();
        await ExpectLeavingBlocked(page, true);

        await SaveItem(page, "wdm_saveslot2.bundle").ClickAsync();
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes");
        await Assertions.Expect(SaveItem(page, "wdm_saveslot2.bundle").Locator("[data-testid='save-unsaved']")).ToHaveCountAsync(0);

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".header-status")).ToContainTextAsync("Saved wdm_saveslot2.bundle");
        await Assertions.Expect(SaveItem(page, "wd2_saveslot1.bundle").Locator("[data-testid='save-unsaved']")).ToBeVisibleAsync();
        await ExpectLeavingBlocked(page, true);

        await SaveItem(page, "wd2_saveslot1.bundle").ClickAsync();
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");
        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".header-status")).ToContainTextAsync("Saved wd2_saveslot1.bundle");
        await Assertions.Expect(page.Locator("[data-testid='save-unsaved']")).ToHaveCountAsync(0);
        await ExpectLeavingBlocked(page, false);
    }

    [Fact]
    public async Task ReopeningTheFolder_AsksBeforeDiscardingUnsavedChanges()
    {
        var page = await OpenTwoSaves();
        await RestartFromEpisode2(page, "wd2_saveslot1.bundle");
        await Assertions.Expect(page.Locator("[data-testid='save-unsaved']")).ToHaveCountAsync(1);

        var messages = new List<string>();
        var accept = false;
        page.Dialog += async (_, dialog) =>
        {
            messages.Add(dialog.Message);
            await (accept ? dialog.AcceptAsync() : dialog.DismissAsync());
        };

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await WaitUntil(() => messages.Count == 1);
        Assert.Contains("wd2_saveslot1.bundle", messages[0]);
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");
        await Assertions.Expect(page.Locator("[data-testid='save-unsaved']")).ToHaveCountAsync(1);

        accept = true;
        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await WaitUntil(() => messages.Count == 2);
        await Assertions.Expect(page.Locator("[data-testid='save-unsaved']")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveCountAsync(0);
        await ExpectLeavingBlocked(page, false);
    }
}
