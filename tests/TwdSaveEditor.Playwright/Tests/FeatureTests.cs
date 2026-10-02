using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class FeatureTests
{
    private readonly PlaywrightFixture _fixture;

    public FeatureTests(PlaywrightFixture fixture) => _fixture = fixture;

    private Task InjectSaveFile(IPage page, string testDataSeason, string fileName)
        => InjectMultipleSaveFiles(page, (testDataSeason, fileName));

    private async Task InjectMultipleSaveFiles(IPage page, params (string season, string fileName)[] files)
    {
        var directory = new Dictionary<string, string>();
        foreach (var (season, fileName) in files)
            await FakeSaveDirectory.AddSaveAsync(directory, season, fileName);

        await FakeSaveDirectory.InstallAsync(page, directory);
    }

    private async Task LoadAndSelectFirstSave(IPage page)
    {
        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await saveItems.First.ClickAsync();

        var decisionEditor = page.Locator(".decision-editor");
        await decisionEditor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task ResumePointTab_ShowsTheCheckpointOfASeason1Save()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S1", "wd1_saveslot2.bundle");
        await LoadAndSelectFirstSave(page);

        var resumeTab = page.Locator("[data-testid='tab-resume']");
        await resumeTab.ClickAsync();

        var state = page.Locator("[data-testid='resume-state']");
        await Assertions.Expect(state).ToContainTextAsync("Episode 4: Around Every Corner");
        await Assertions.Expect(state).ToContainTextAsync("missingClementine");

        var episodes = page.Locator("[data-testid='restart-episode'] option");
        await Assertions.Expect(episodes).ToHaveCountAsync(6);
    }

    [Fact]
    public async Task ResumePointTab_ShowsTheEpisodeInProgressForSeason2()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S2", "wd2_saveslot1.bundle");
        await LoadAndSelectFirstSave(page);

        var resumeTab = page.Locator("[data-testid='tab-resume']");
        await resumeTab.ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("Episode 1: All That Remains");
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter'] option")).ToHaveCountAsync(23);
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter'] optgroup")).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task InventoryTab_ShowsWhatClementineHoldsInSeason2()
    {
        var page = await _fixture.NewPage();
        await InjectMultipleSaveFiles(page, ("S2", "wd2_saveslot1.bundle"), ("S2", "_wd2_saveslot1_autosave.bundle"));
        await LoadAndSelectFirstSave(page);

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='inventory-summary']")).ToContainTextAsync("Episode 1: All That Remains, 2 items");
        await Assertions.Expect(page.Locator(".inventory-item")).ToHaveCountAsync(13);
        await Assertions.Expect(page.Locator("[data-testid='inventory-item-ui_item_watch']")).ToBeCheckedAsync();
        await Assertions.Expect(page.Locator("[data-testid='inventory-item-ui_item_hammer']")).ToBeCheckedAsync();
        await Assertions.Expect(page.Locator(".inventory-item input:checked")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task InventoryTab_ShowsWhatLeeHoldsInSeason1()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S1", "wd1_saveslot2.bundle");
        await LoadAndSelectFirstSave(page);

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='inventory-summary']")).ToContainTextAsync("Lee");
        await Assertions.Expect(page.Locator("[data-testid='inventory-summary']")).ToContainTextAsync("Episode 4: Around Every Corner, 1 item");
        await Assertions.Expect(page.Locator(".inventory-item")).ToHaveCountAsync(16);
        await Assertions.Expect(page.Locator("[data-testid='inventory-item-Inventory - Locker Combination']")).ToBeCheckedAsync();
        await Assertions.Expect(page.Locator(".inventory-item input:checked")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-testid='inventory-carried']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Season3Save_ListsItsDecisionsAndChapters()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S3", "wd3_saveslot1.bundle");
        await LoadAndSelectFirstSave(page);

        await Assertions.Expect(page.Locator("[data-testid='season-s3'] > summary .badge")).ToContainTextAsync("49 choices");
        await Assertions.Expect(page.GetByText("Carried over from the previous season")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-testid='season-s2']")).ToHaveCountAsync(0);

        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("Episode 1: Ties That Bind - Part One");
        await Assertions.Expect(page.Locator("[data-testid='restart-episode'] option")).ToHaveCountAsync(5);
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter'] optgroup")).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task InventoryTab_ShowsWhatJavierHoldsInSeason3()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S3", "wd3_saveslot1.bundle");
        await LoadAndSelectFirstSave(page);

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='inventory-summary']")).ToContainTextAsync("Javier");
        await Assertions.Expect(page.Locator(".inventory-item")).ToHaveCountAsync(4);
        await Assertions.Expect(page.Locator(".inventory-item input:checked")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-testid='inventory-item-Inventory - Candy Bar']")).ToBeCheckedAsync();
        await Assertions.Expect(page.Locator("[data-testid='inventory-carried']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task InventoryTab_SaysWhenASeasonHasNoEditableInventory()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S4", "wd4_saveslot1.bundle");
        await LoadAndSelectFirstSave(page);

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='inventory-unsupported']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".inventory-item")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task S4Save_ShowsPresetButtons()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S4", "wd4_saveslot1.bundle");
        await LoadAndSelectFirstSave(page);

        var decisionsTab = page.Locator("[data-testid='tab-decisions']");
        await decisionsTab.ClickAsync();

        var saveLouisBtn = page.GetByRole(AriaRole.Button, new() { Name = "Save Louis Path" });
        await Assertions.Expect(saveLouisBtn).ToBeVisibleAsync();

        var saveVioletBtn = page.GetByRole(AriaRole.Button, new() { Name = "Save Violet Path" });
        await Assertions.Expect(saveVioletBtn).ToBeVisibleAsync();

        var trustAjBtn = page.GetByRole(AriaRole.Button, new() { Name = "Trust AJ Path" });
        await Assertions.Expect(trustAjBtn).ToBeVisibleAsync();
    }

    [Fact]
    public async Task S1AndS2Loaded_ShowsImportButton()
    {
        var page = await _fixture.NewPage();
        await InjectMultipleSaveFiles(page,
            ("S1", "wd1_saveslot2.bundle"),
            ("S2", "wd2_saveslot1.bundle"));

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var s2Save = page.Locator(".save-item", new() { HasText = "wd2" });
        if (await s2Save.CountAsync() == 0)
        {
            s2Save = page.Locator(".save-item", new() { HasText = "Season 2" });
        }

        if (await s2Save.CountAsync() == 0)
        {
            s2Save = saveItems.Nth(1);
        }

        await s2Save.First.ClickAsync();

        var decisionEditor = page.Locator(".decision-editor");
        await decisionEditor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var decisionsTab = page.Locator("[data-testid='tab-decisions']");
        await decisionsTab.ClickAsync();

        var importBtn = page.GetByRole(AriaRole.Button, new() { Name = "Import" });
        await Assertions.Expect(importBtn).ToBeVisibleAsync();
    }
}
