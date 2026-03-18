using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright;

[Collection(PlaywrightCollection.Name)]
public class SaveLoadCycleTests
{
    private readonly PlaywrightFixture _fixture;

    public SaveLoadCycleTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Inject a save file from TestData by overriding the fileSystemApi in the browser
    /// to serve the file as if it were picked from a directory.
    /// </summary>
    private async Task InjectSaveFile(IPage page, string testDataSeason, string fileName)
    {
        var filePath = TestDataHelper.GetPath(testDataSeason, fileName);
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(fileBytes);

        // Also collect estore/epage files if they exist
        var seasonDir = TestDataHelper.GetSeasonDir(testDataSeason);
        var bundleBase = Path.GetFileNameWithoutExtension(fileName);
        var estoreName = $"_{bundleBase}_id.estore";
        var estorePath = Path.Combine(seasonDir, estoreName);

        var filesJs = $"'{fileName}': '{base64}'";

        if (File.Exists(estorePath))
        {
            var estoreBytes = await File.ReadAllBytesAsync(estorePath);
            var estoreBase64 = Convert.ToBase64String(estoreBytes);
            filesJs += $", '{estoreName}': '{estoreBase64}'";

            // Find epage files
            var epagePattern = $"_{bundleBase}_id_Page*.epage";
            var epageFiles = Directory.GetFiles(seasonDir, epagePattern);
            foreach (var epagePath in epageFiles.OrderBy(f => f))
            {
                var epageName = Path.GetFileName(epagePath);
                var epageBytes = await File.ReadAllBytesAsync(epagePath);
                var epageBase64 = Convert.ToBase64String(epageBytes);
                filesJs += $", '{epageName}': '{epageBase64}'";
            }
        }

        // Override the fileSystemApi to serve our test files
        await page.EvaluateAsync($@"() => {{
            const testFiles = {{ {filesJs} }};
            window.fileSystemApi = {{
                isSupported: () => true,
                pickDirectory: async () => true,
                hasDirectory: () => true,
                getDirectoryName: () => 'TestData',
                listFiles: async (ext) => {{
                    return Object.keys(testFiles).filter(n => n.endsWith(ext));
                }},
                readFile: async (name) => {{
                    return testFiles[name] || null;
                }},
                readFileBytes: async (name) => {{
                    const b64 = testFiles[name];
                    if (!b64) return null;
                    const binary = atob(b64);
                    const bytes = new Uint8Array(binary.length);
                    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
                    return bytes;
                }},
                writeFile: async (name, bytesBase64) => {{
                    testFiles[name] = bytesBase64;
                    return true;
                }},
                writeFileBytes: async (name, bytes) => {{
                    let binary = '';
                    for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
                    testFiles[name] = btoa(binary);
                    return true;
                }},
                backupFiles: async (folderName, fileNames) => true
            }};
        }}");
    }

    /// <summary>
    /// Inject multiple save files. Each entry can be "fileName" or "diskName:injectName"
    /// to rename on injection (e.g. "wd1_saveslot1_live.bundle:wd1_saveslot1.bundle").
    /// </summary>
    private async Task InjectSaveFiles(IPage page, string testDataSeason, params string[] fileSpecs)
    {
        var filesJsParts = new List<string>();

        foreach (var spec in fileSpecs)
        {
            string diskName, injectName;
            if (spec.Contains(':'))
            {
                var parts = spec.Split(':', 2);
                diskName = parts[0];
                injectName = parts[1];
            }
            else
            {
                diskName = spec;
                injectName = spec;
            }

            var filePath = TestDataHelper.GetPath(testDataSeason, diskName);
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var base64 = Convert.ToBase64String(fileBytes);
            filesJsParts.Add($"'{injectName}': '{base64}'");
        }

        var filesJs = string.Join(", ", filesJsParts);

        await page.EvaluateAsync($@"() => {{
            const testFiles = {{ {filesJs} }};
            window.fileSystemApi = {{
                isSupported: () => true,
                pickDirectory: async () => true,
                hasDirectory: () => true,
                getDirectoryName: () => 'TestData',
                listFiles: async (ext) => Object.keys(testFiles).filter(n => n.endsWith(ext)),
                readFile: async (name) => testFiles[name] || null,
                readFileBytes: async (name) => {{
                    const b64 = testFiles[name];
                    if (!b64) return null;
                    const binary = atob(b64);
                    const bytes = new Uint8Array(binary.length);
                    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
                    return bytes;
                }},
                writeFile: async (name, bytesBase64) => {{ testFiles[name] = bytesBase64; return true; }},
                writeFileBytes: async (name, bytes) => {{
                    let binary = '';
                    for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
                    testFiles[name] = btoa(binary);
                    return true;
                }},
                backupFiles: async (folderName, fileNames) => true
            }};
        }}");
    }

    [Fact]
    public async Task LoadS1Save_ShowsDecisions()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S1", "wd1_saveslot2.bundle");

        // Trigger directory pick via the UI
        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        // Wait for save list to populate
        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Click on the save file
        await saveItems.First.ClickAsync();

        // Wait for decisions to load
        var decisionEditor = page.Locator(".decision-editor");
        await decisionEditor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Verify choices are displayed
        await Assertions.Expect(decisionEditor).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoadS1Autosave_DoesNotCrash()
    {
        var page = await _fixture.NewPage();

        // Capture console errors
        var consoleErrors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                consoleErrors.Add(msg.Text);
        };

        await InjectSaveFile(page, "S1", "_wd1_saveslot1_autosave.bundle");

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        // Wait for loading to complete
        await page.WaitForTimeoutAsync(5000);

        // Check if save appeared in list
        var saveItems = page.Locator(".save-item");
        var saveCount = await saveItems.CountAsync();

        // Check for error toasts
        var errorToasts = page.Locator(".toast-error");
        var errorCount = await errorToasts.CountAsync();

        // Dump diagnostics if save didn't load
        if (saveCount == 0)
        {
            var statusText = await page.Locator(".header-status").TextContentAsync();
            var toastTexts = new List<string>();
            for (int i = 0; i < errorCount; i++)
                toastTexts.Add(await errorToasts.Nth(i).TextContentAsync() ?? "");

            Assert.Fail(
                $"Autosave didn't load. Status: '{statusText}'. " +
                $"Error toasts ({errorCount}): [{string.Join(", ", toastTexts)}]. " +
                $"Console errors ({consoleErrors.Count}): [{string.Join(", ", consoleErrors.Take(5))}]");
        }

        // Click on the autosave
        await saveItems.First.ClickAsync();
        await page.WaitForTimeoutAsync(1000);

        // Verify page is still responsive
        var appReady = page.Locator("[data-testid='app-ready']");
        await Assertions.Expect(appReady).ToBeVisibleAsync();

        // Tabs should appear
        var tabs = page.Locator(".tab-bar button");
        var tabCount = await tabs.CountAsync();
        Assert.True(tabCount >= 3, $"Expected tabs after selecting autosave, got {tabCount}. Console errors: [{string.Join(", ", consoleErrors.Take(3))}]");
    }

    [Fact]
    public async Task EditAutosaveMetadata_SaveAndReload()
    {
        var page = await _fixture.NewPage();

        var consoleErrors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                consoleErrors.Add(msg.Text);
        };

        // Inject BOTH the autosave and its slot bundle (game needs both)
        await InjectSaveFiles(page, "S1",
            "_wd1_saveslot1_autosave.bundle",
            "wd1_saveslot1_live.bundle:wd1_saveslot1.bundle");

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        // Wait for saves to appear and click the autosave (not the slot)
        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Find the autosave entry
        var autosaveItem = page.Locator(".save-item", new() { HasTextString = "_autosave" });
        if (await autosaveItem.CountAsync() == 0)
            autosaveItem = page.Locator(".save-item", new() { HasTextString = "autosave" });
        if (await autosaveItem.CountAsync() == 0)
        {
            // Fallback: just click each save item until we find the autosave
            for (int i = 0; i < await saveItems.CountAsync(); i++)
            {
                var text = await saveItems.Nth(i).TextContentAsync();
                if (text?.Contains("autosave", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await saveItems.Nth(i).ClickAsync();
                    break;
                }
            }
        }
        else
        {
            await autosaveItem.First.ClickAsync();
        }

        // Switch to Resume Point tab
        var resumeTab = page.Locator(".tab-bar button", new() { HasTextString = "Resume" });
        await resumeTab.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        // Verify resume editor is visible with autosave fields
        var resumeEditor = page.Locator(".resume-editor");
        await Assertions.Expect(resumeEditor).ToBeVisibleAsync();

        // Should show "Autosave / Checkpoint" type
        var typeField = resumeEditor.Locator(".field-value", new() { HasTextString = "Autosave" });
        await Assertions.Expect(typeField).ToBeVisibleAsync();

        // Change episode dropdown
        var episodeSelect = resumeEditor.Locator("select").First;
        await episodeSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await page.WaitForTimeoutAsync(500);

        // Click save
        var saveBtn = page.Locator(".save-btn");
        await saveBtn.ClickAsync();
        await page.WaitForTimeoutAsync(2000);

        // Check for errors after save
        var errorToasts = page.Locator(".toast-error");
        var errorCount = await errorToasts.CountAsync();
        if (errorCount > 0)
        {
            var toastText = await errorToasts.First.TextContentAsync();
            Assert.Fail($"Save produced error: {toastText}. Console: [{string.Join(", ", consoleErrors.Take(3))}]");
        }

        // Read the saved autosave bytes back from the fake FS
        var savedBase64 = await page.EvaluateAsync<string?>(
            "() => window.fileSystemApi.readFile('_wd1_saveslot1_autosave.bundle')");
        Assert.NotNull(savedBase64);
        var savedBytes = Convert.FromBase64String(savedBase64);
        Assert.True(savedBytes.Length > 0, "Saved file is empty");

        // Verify the saved autosave can be parsed
        var reloaded = TwdSaveEditor.Core.Binary.BundleReader.Read(savedBytes, "_wd1_saveslot1_autosave.bundle");
        Assert.NotNull(reloaded.Metadata);

        // Verify the slot bundle's episode ID was also updated
        var slotBase64 = await page.EvaluateAsync<string?>(
            "() => window.fileSystemApi.readFile('wd1_saveslot1.bundle')");
        Assert.NotNull(slotBase64);
        var slotBytes = Convert.FromBase64String(slotBase64);
        var slotReloaded = TwdSaveEditor.Core.Binary.BundleReader.Read(slotBytes, "wd1_saveslot1.bundle");
        Assert.NotNull(slotReloaded.Metadata);

        // The slot should have the episode ID property (0xB218E7C003A67CE9)
        var slotEpProp = slotReloaded.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.NotNull(slotEpProp);
        // It should NOT be "WalkingDead101" (we changed the episode)
        var slotEpValue = ((TwdSaveEditor.Core.Model.StringValue)slotEpProp.Value).Value;
        Assert.NotEqual("WalkingDead101", slotEpValue);

        // The slot should have the progress integer updated (this is what the game menu reads)
        var slotProgressProp = slotReloaded.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0x94C245DACB1ADDC3);
        Assert.NotNull(slotProgressProp);
        Assert.NotEqual(1, ((TwdSaveEditor.Core.Model.IntValue)slotProgressProp.Value).Value);

        // Now simulate a full reload: re-inject the SAVED bytes and verify it loads in the UI
        var reinjectBase64 = Convert.ToBase64String(savedBytes);
        await page.EvaluateAsync($@"() => {{
            const testFiles = {{ '_wd1_saveslot1_autosave.bundle': '{reinjectBase64}' }};
            window.fileSystemApi = {{
                isSupported: () => true,
                pickDirectory: async () => true,
                hasDirectory: () => true,
                getDirectoryName: () => 'TestData',
                listFiles: async (ext) => Object.keys(testFiles).filter(n => n.endsWith(ext)),
                readFile: async (name) => testFiles[name] || null,
                readFileBytes: async (name) => {{
                    const b64 = testFiles[name];
                    if (!b64) return null;
                    const binary = atob(b64);
                    const bytes = new Uint8Array(binary.length);
                    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
                    return bytes;
                }},
                writeFile: async (name, bytesBase64) => {{ testFiles[name] = bytesBase64; return true; }},
                writeFileBytes: async (name, bytes) => {{ return true; }}
            }};
        }}");

        // Reload directory with the saved file
        await openDirBtn.ClickAsync();
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Click the save again
        await saveItems.First.ClickAsync();
        await page.WaitForTimeoutAsync(1000);

        // Should still show tabs without crashing
        var tabs = page.Locator(".tab-bar button");
        var tabCount = await tabs.CountAsync();
        Assert.True(tabCount >= 3,
            $"Expected tabs after reloading edited autosave, got {tabCount}. " +
            $"Console errors: [{string.Join(", ", consoleErrors.Take(5))}]");
    }

    [Fact]
    public async Task LoadS4Save_ShowsDecisions()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S4", "wd4_saveslot1.bundle");

        // Trigger directory pick via the UI
        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        // Wait for save list to populate
        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Click on the save file
        await saveItems.First.ClickAsync();

        // Wait for decisions to load
        var decisionEditor = page.Locator(".decision-editor");
        await decisionEditor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Verify choices are displayed
        await Assertions.Expect(decisionEditor).ToBeVisibleAsync();
    }
}
