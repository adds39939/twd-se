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
            var path = TestDataHelper.GetPath(diskName.Contains("wd2") ? "S2" : "S1", diskName);
            directory[injectName] = Convert.ToBase64String(await File.ReadAllBytesAsync(path));
        }

        await FakeSaveDirectory.InstallAsync(page, directory);
    }

    private static async Task InjectSeason(IPage page, string season, params string[] fileNames)
    {
        var directory = new Dictionary<string, string>();
        foreach (var name in fileNames)
        {
            directory[name] = Convert.ToBase64String(await File.ReadAllBytesAsync(TestDataHelper.GetPath(season, name)));
        }

        await FakeSaveDirectory.InstallAsync(page, directory);
    }

    private static async Task<Dictionary<string, string?>> ReadAll(IPage page)
    {
        var files = new Dictionary<string, string?>();
        foreach (var name in await FakeSaveDirectory.GetFileNamesAsync(page))
        {
            files[name] = await FakeSaveDirectory.ReadFileAsync(page, name);
        }

        return files;
    }

    [Fact]
    public async Task SaveSlotWithAutosave_BackupsBothFiles()
    {
        var page = await _fixture.NewPage();

        await InjectWithBackupTracking(page,
            ("wd2_saveslot1.bundle", "wd2_saveslot1.bundle"),
            ("_wd2_saveslot1_autosave.bundle", "_wd2_saveslot1_autosave.bundle"));

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
        Assert.Contains("_wd2_saveslot1_autosave.bundle", files);
        Assert.Contains("wd2_saveslot1.bundle", files);
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

    [Fact]
    public async Task RestartingAnEpisode_BacksUpTheFilesItDeletes()
    {
        var page = await _fixture.NewPage();
        await InjectSeason(page, "Michonne",
            "wdm_saveslot2.bundle", "_wdm_saveslot2_autosave.bundle", "_wdm_saveslot2_checkpoint1.bundle",
            "_wdm_saveslot2_id.estore", "_wdm_saveslot2_id_Page969.epage", "_wdm_saveslot2_id_Page1963.epage");

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        var firstChapter = await page.Locator("[data-testid='restart-chapter'] option").First.GetAttributeAsync("value");
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync(firstChapter!);
        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("from the beginning");

        var before = await FakeSaveDirectory.GetFileNamesAsync(page);
        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".header-status")).ToContainTextAsync("Saved wdm_saveslot2.bundle");

        var deleted = before.Except(await FakeSaveDirectory.GetFileNamesAsync(page)).ToArray();
        var backup = await FakeSaveDirectory.GetLastBackupFilesAsync(page);
        Assert.NotEmpty(deleted);
        Assert.NotNull(backup);
        Assert.All(deleted, name => Assert.Contains(name, backup));
    }

    [Fact]
    public async Task FailedBackup_LeavesEveryFileUntouched()
    {
        var page = await _fixture.NewPage();
        await InjectSeason(page, "S2",
            "wd2_saveslot1.bundle", "_wd2_saveslot1_autosave.bundle", "_wd2_saveslot1_id.estore",
            "_wd2_saveslot1_id_Page913.epage", "_wd2_saveslot1_id_Page1897.epage", "_wd2_saveslot1_id_Page2734.epage");

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("2");
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");

        var before = await ReadAll(page);
        await FakeSaveDirectory.BlockBackupsAsync(page);
        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".header-status")).ToContainTextAsync("Backup failed");

        Assert.Equal(before, await ReadAll(page));
        Assert.Null(await FakeSaveDirectory.GetLastBackupFolderAsync(page));
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");
        await Assertions.Expect(page.Locator("[data-testid='save-unsaved']")).ToHaveCountAsync(1);
    }
}
