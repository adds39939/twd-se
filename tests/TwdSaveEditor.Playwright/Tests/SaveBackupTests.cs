using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class SaveBackupTests
{
    private readonly PlaywrightFixture _fixture;
    public SaveBackupTests(PlaywrightFixture fixture) => _fixture = fixture;

    private static async Task InjectWithBackupTracking(IPage page, params (string diskName, string injectName)[] fileSpecs)
    {
        var directory = new Dictionary<string, string>();
        foreach (var (diskName, injectName) in fileSpecs)
        {
            var path = TestDataHelper.GetPath("S1", diskName);
            directory[injectName] = Convert.ToBase64String(await File.ReadAllBytesAsync(path));
        }

        await FakeSaveDirectory.InstallAsync(page, directory);
    }

    [Fact]
    public async Task SaveAutosave_BackupsBothFiles()
    {
        var page = await _fixture.NewPage();

        await InjectWithBackupTracking(page,
            ("wd1_saveslot1_live.bundle", "wd1_saveslot1.bundle"),
            ("_wd1_saveslot1_autosave.bundle", "_wd1_saveslot1_autosave.bundle"));

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var autosaveItem = page.Locator(".save-item", new PageLocatorOptions { HasTextString = "autosave" });
        await autosaveItem.First.ClickAsync();

        var saveBtn = page.Locator(".save-btn");
        await saveBtn.ClickAsync();
        await page.WaitForTimeoutAsync(2000);

        var folder = await FakeSaveDirectory.GetLastBackupFolderAsync(page);
        var files = await FakeSaveDirectory.GetLastBackupFilesAsync(page);

        Assert.NotNull(folder);
        Assert.NotNull(files);
        Assert.StartsWith("backup_", folder);
        Assert.Contains("_wd1_saveslot1_autosave.bundle", files);
        Assert.Contains("wd1_saveslot1.bundle", files);
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public async Task SaveSlotBundle_BackupsOnlySlot()
    {
        var page = await _fixture.NewPage();

        await InjectWithBackupTracking(page,
            ("wd1_saveslot1_live.bundle", "wd1_saveslot1.bundle"));

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await saveItems.First.ClickAsync();

        var saveBtn = page.Locator(".save-btn");
        await saveBtn.ClickAsync();
        await page.WaitForTimeoutAsync(2000);

        var folder = await FakeSaveDirectory.GetLastBackupFolderAsync(page);
        var files = await FakeSaveDirectory.GetLastBackupFilesAsync(page);

        Assert.NotNull(folder);
        Assert.NotNull(files);
        Assert.StartsWith("backup_", folder);
        Assert.Single(files);
        Assert.Equal("wd1_saveslot1.bundle", files[0]);
    }
}
