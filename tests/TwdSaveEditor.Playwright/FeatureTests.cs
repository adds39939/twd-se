using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright;

[Collection(PlaywrightCollection.Name)]
public class FeatureTests
{
    private readonly PlaywrightFixture _fixture;

    public FeatureTests(PlaywrightFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Inject a single save file from TestData by overriding the fileSystemApi in the browser.
    /// </summary>
    private async Task InjectSaveFile(IPage page, string testDataSeason, string fileName)
    {
        var filePath = TestDataHelper.GetPath(testDataSeason, fileName);
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(fileBytes);

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

        await page.EvaluateAsync($$"""
           () => {
                       const testFiles = { {{filesJs}} };
                       window.fileSystemApi = {
                           isSupported: () => true,
                           pickDirectory: async () => true,
                           hasDirectory: () => true,
                           getDirectoryName: () => 'TestData',
                           listFiles: async (ext) => {
                               return Object.keys(testFiles).filter(n => n.endsWith(ext));
                           },
                           readFile: async (name) => {
                               return testFiles[name] || null;
                           },
                           readFileBytes: async (name) => {
                               const b64 = testFiles[name];
                               if (!b64) return null;
                               const binary = atob(b64);
                               const bytes = new Uint8Array(binary.length);
                               for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
                               return bytes;
                           },
                           writeFile: async (name, bytesBase64) => {
                               testFiles[name] = bytesBase64;
                               return true;
                           },
                           writeFileBytes: async (name, bytes) => {
                               let binary = '';
                               for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
                               testFiles[name] = btoa(binary);
                               return true;
                           }
                       };
                   }
           """);
    }

    /// <summary>
    /// Inject multiple save files from different seasons into the fake fileSystemApi.
    /// </summary>
    private async Task InjectMultipleSaveFiles(IPage page, params (string season, string fileName)[] files)
    {
        var filesJs = "";
        foreach (var (season, fileName) in files)
        {
            var filePath = TestDataHelper.GetPath(season, fileName);
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var base64 = Convert.ToBase64String(fileBytes);

            if (filesJs.Length > 0) filesJs += ", ";
            filesJs += $"'{fileName}': '{base64}'";

            var seasonDir = TestDataHelper.GetSeasonDir(season);
            var bundleBase = Path.GetFileNameWithoutExtension(fileName);
            var estoreName = $"_{bundleBase}_id.estore";
            var estorePath = Path.Combine(seasonDir, estoreName);

            if (File.Exists(estorePath))
            {
                var estoreBytes = await File.ReadAllBytesAsync(estorePath);
                var estoreBase64 = Convert.ToBase64String(estoreBytes);
                filesJs += $", '{estoreName}': '{estoreBase64}'";

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
        }

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
                }}
            }};
        }}");
    }

    /// <summary>
    /// Load saves and click the first save item.
    /// </summary>
    private async Task LoadAndSelectFirstSave(IPage page)
    {
        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await saveItems.First.ClickAsync();

        // Wait for the editor to load
        var decisionEditor = page.Locator(".decision-editor");
        await decisionEditor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
    }

    [Fact]
    public async Task ResumePointTab_ShowsPlaytimeField()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S1", "wd1_saveslot2.bundle");
        await LoadAndSelectFirstSave(page);

        // Switch to Resume Point tab
        var resumeTab = page.Locator("[data-testid='tab-resume']");
        await resumeTab.ClickAsync();

        // Verify a number input with label "Playtime" exists
        var playtimeLabel = page.Locator("label:has-text('Playtime')");
        await Assertions.Expect(playtimeLabel).ToBeVisibleAsync();

        var playtimeInput = page.Locator("input[type='number']").First;
        await Assertions.Expect(playtimeInput).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ResumePointTab_ShowsAutosaveFileField()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S1", "wd1_saveslot2.bundle");
        await LoadAndSelectFirstSave(page);

        // Switch to Resume Point tab
        var resumeTab = page.Locator("[data-testid='tab-resume']");
        await resumeTab.ClickAsync();

        // Verify text input with label "Autosave File" exists
        var label = page.Locator("label:has-text('Autosave File')");
        await Assertions.Expect(label).ToBeVisibleAsync();
    }

    [Fact]
    public async Task S4Save_ShowsPresetButtons()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S4", "wd4_saveslot1.bundle");
        await LoadAndSelectFirstSave(page);

        // Switch to Decisions tab (may already be active, but click to be sure)
        var decisionsTab = page.Locator("[data-testid='tab-decisions']");
        await decisionsTab.ClickAsync();

        // Verify preset buttons exist
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

        // Wait for save list to populate
        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Click the S2 save (find the item that contains "wd2" or "Season 2" text)
        var s2Save = page.Locator(".save-item", new() { HasText = "wd2" });
        if (await s2Save.CountAsync() == 0)
        {
            // Fallback: try matching by season label
            s2Save = page.Locator(".save-item", new() { HasText = "Season 2" });
        }

        if (await s2Save.CountAsync() == 0)
        {
            // Last fallback: click the second item (S2 sorts after S1)
            s2Save = saveItems.Nth(1);
        }

        await s2Save.First.ClickAsync();

        // Wait for the editor to load
        var decisionEditor = page.Locator(".decision-editor");
        await decisionEditor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // Switch to Decisions tab
        var decisionsTab = page.Locator("[data-testid='tab-decisions']");
        await decisionsTab.ClickAsync();

        // Verify "Import" button exists
        var importBtn = page.GetByRole(AriaRole.Button, new() { Name = "Import" });
        await Assertions.Expect(importBtn).ToBeVisibleAsync();
    }
}
