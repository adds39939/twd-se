using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright;

[Collection(PlaywrightCollection.Name)]
public class SaveBackupTests
{
    private readonly PlaywrightFixture _fixture;
    public SaveBackupTests(PlaywrightFixture fixture) => _fixture = fixture;

    private static async Task InjectWithBackupTracking(IPage page, params (string diskName, string injectName)[] fileSpecs)
    {
        var filesJsParts = new List<string>();
        foreach (var (diskName, injectName) in fileSpecs)
        {
            var path = TestDataHelper.GetPath("S1", diskName);
            var b64 = Convert.ToBase64String(await File.ReadAllBytesAsync(path));
            filesJsParts.Add($"'{injectName}': '{b64}'");
        }
        var filesJs = string.Join(", ", filesJsParts);

        await page.EvaluateAsync($@"() => {{
            const testFiles = {{ {filesJs} }};
            window._lastBackup = null;
            window.fileSystemApi = {{
                isSupported: () => true,
                pickDirectory: async () => true,
                hasDirectory: () => true,
                getDirectoryName: () => 'TestData',
                listFiles: async (ext) => Object.keys(testFiles).filter(n => n.endsWith(ext)),
                readFile: async (name) => testFiles[name] || null,
                writeFile: async (name, bytesBase64) => {{ testFiles[name] = bytesBase64; return true; }},
                writeFileBytes: async (name, bytes) => {{ return true; }},
                backupFiles: async (folderName, fileNames) => {{
                    window._lastBackup = {{ folder: folderName, files: Array.from(fileNames) }};
                    return true;
                }}
            }};
        }}");
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

        // Select the autosave
        var autosaveItem = page.Locator(".save-item", new PageLocatorOptions { HasTextString = "autosave" });
        await autosaveItem.First.ClickAsync();

        // Save
        var saveBtn = page.Locator(".save-btn");
        await saveBtn.ClickAsync();
        await page.WaitForTimeoutAsync(2000);

        // Check backup was created — read the values separately to avoid deserialization issues
        var folder = await page.EvaluateAsync<string?>("() => window._lastBackup?.folder");
        var files = await page.EvaluateAsync<string[]?>("() => window._lastBackup?.files");

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

        var folder = await page.EvaluateAsync<string?>("() => window._lastBackup?.folder");
        var files = await page.EvaluateAsync<string[]?>("() => window._lastBackup?.files");

        Assert.NotNull(folder);
        Assert.NotNull(files);
        Assert.StartsWith("backup_", folder);
        Assert.Single(files);
        Assert.Equal("wd1_saveslot1.bundle", files[0]);
    }
}
