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
                }}
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
