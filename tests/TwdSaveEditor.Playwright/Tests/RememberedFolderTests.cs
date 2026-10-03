using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class RememberedFolderTests : IAsyncLifetime
{
    private const string FolderName = "The Walking Dead Definitive";

    private const string AddToPrivateFolder = """
        async ([folderName, files]) => {
            const root = await navigator.storage.getDirectory();
            const folder = await root.getDirectoryHandle(folderName, { create: true });
            for (const [name, base64] of Object.entries(files)) {
                const writable = await (await folder.getFileHandle(name, { create: true })).createWritable();
                await writable.write(Uint8Array.from(atob(base64), c => c.charCodeAt(0)));
                await writable.close();
            }
            window.showDirectoryPicker = async () => folder;
        }
        """;

    private const string AskForPermission = "FileSystemHandle.prototype.queryPermission = async () => 'prompt';";

    private readonly PlaywrightFixture _fixture;
    private readonly string _profile = Directory.CreateTempSubdirectory().FullName;
    private IPlaywright _playwright = null!;
    private IBrowserContext _profileBrowser = null!;

    public RememberedFolderTests(PlaywrightFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _profileBrowser = await _playwright.Chromium.LaunchPersistentContextAsync(_profile, new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await _profileBrowser.DisposeAsync();
        _playwright.Dispose();
        Directory.Delete(_profile, true);
    }

    private static async Task AddSaveAsync(IPage page, string season, string fileName)
    {
        var files = new Dictionary<string, string>();
        await FakeSaveDirectory.AddSaveAsync(files, season, fileName);
        await page.EvaluateAsync(AddToPrivateFolder, new object[] { FolderName, files });
    }

    private async Task<IPage> OpenFolderAsync()
    {
        var page = await _profileBrowser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForSelectorAsync("[data-testid='app-ready']", new() { Timeout = 30000 });
        await AddSaveAsync(page, "S1", "wd1_saveslot2.bundle");
        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(1);
        return page;
    }

    [Fact]
    public async Task OpenedFolder_IsLoadedAgainOnTheNextVisit()
    {
        var page = await OpenFolderAsync();

        await page.ReloadAsync();

        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator(".dir-name")).ToHaveTextAsync(FolderName);
    }

    [Fact]
    public async Task RememberedFolderThatNeedsPermission_IsOfferedForReopening()
    {
        var page = await OpenFolderAsync();
        await page.AddInitScriptAsync(AskForPermission);

        await page.ReloadAsync();

        var reopen = page.Locator("[data-testid='reopen-directory']");
        await Assertions.Expect(reopen).ToHaveTextAsync($"Reopen {FolderName}");
        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(0);
        await reopen.ClickAsync();
        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(1);
        await Assertions.Expect(reopen).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Reload_ReadsTheFolderAgainAndKeepsTheSelectedSave()
    {
        var page = await OpenFolderAsync();
        await page.Locator(".save-item").First.ClickAsync();
        await AddSaveAsync(page, "S3", "wd3_saveslot1.bundle");

        await page.Locator("[data-testid='reload-directory']").ClickAsync();

        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(2);
        await Assertions.Expect(page.Locator(".save-item.selected .save-name")).ToHaveTextAsync("wd1_saveslot2.bundle");
    }
}
