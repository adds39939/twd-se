using Microsoft.Playwright;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class SaveLoadCycleTests
{
    private readonly PlaywrightFixture _fixture;

    public SaveLoadCycleTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task InjectSaveFile(IPage page, string testDataSeason, string fileName)
    {
        var directory = new Dictionary<string, string>();
        await FakeSaveDirectory.AddSaveAsync(directory, testDataSeason, fileName);
        await FakeSaveDirectory.InstallAsync(page, directory);
    }

    private async Task InjectSaveFiles(IPage page, string testDataSeason, params string[] fileSpecs)
    {
        var directory = new Dictionary<string, string>();

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

            var fileBytes = await File.ReadAllBytesAsync(TestDataHelper.GetPath(testDataSeason, diskName));
            directory[injectName] = Convert.ToBase64String(fileBytes);
        }

        await FakeSaveDirectory.InstallAsync(page, directory);
    }

    [Fact]
    public async Task LoadS1Save_ShowsDecisions()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S1", "wd1_saveslot2.bundle");

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        await saveItems.First.ClickAsync();

        var decisionEditor = page.Locator(".decision-editor");
        await decisionEditor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        await Assertions.Expect(decisionEditor).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoadS2Autosave_DoesNotCrash()
    {
        var page = await _fixture.NewPage();

        var consoleErrors = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                consoleErrors.Add(msg.Text);
        };

        await InjectSaveFiles(page, "S2", "_wd2_saveslot1_autosave.bundle");

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        await page.WaitForTimeoutAsync(5000);

        var saveItems = page.Locator(".save-item");
        var saveCount = await saveItems.CountAsync();

        var errorToasts = page.Locator(".toast-error");
        var errorCount = await errorToasts.CountAsync();

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

        await saveItems.First.ClickAsync();
        await page.WaitForTimeoutAsync(1000);

        var appReady = page.Locator("[data-testid='app-ready']");
        await Assertions.Expect(appReady).ToBeVisibleAsync();

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

        await InjectSaveFiles(page, "S2",
            "_wd2_saveslot1_autosave.bundle",
            "wd2_saveslot1.bundle");

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        var autosaveItem = page.Locator(".save-item", new() { HasTextString = "_autosave" });
        if (await autosaveItem.CountAsync() == 0)
            autosaveItem = page.Locator(".save-item", new() { HasTextString = "autosave" });
        if (await autosaveItem.CountAsync() == 0)
        {
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

        var resumeTab = page.Locator(".tab-bar button", new() { HasTextString = "Resume" });
        await resumeTab.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        var resumeEditor = page.Locator(".resume-editor");
        await Assertions.Expect(resumeEditor).ToBeVisibleAsync();

        var typeField = resumeEditor.Locator(".field-value", new() { HasTextString = "Autosave" });
        await Assertions.Expect(typeField).ToBeVisibleAsync();

        var episodeSelect = resumeEditor.Locator("select").First;
        await episodeSelect.SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await page.WaitForTimeoutAsync(500);

        var saveBtn = page.Locator(".save-btn");
        await saveBtn.ClickAsync();
        await page.WaitForTimeoutAsync(2000);

        var errorToasts = page.Locator(".toast-error");
        var errorCount = await errorToasts.CountAsync();
        if (errorCount > 0)
        {
            var toastText = await errorToasts.First.TextContentAsync();
            Assert.Fail($"Save produced error: {toastText}. Console: [{string.Join(", ", consoleErrors.Take(3))}]");
        }

        var savedBase64 = await FakeSaveDirectory.ReadFileAsync(page, "_wd2_saveslot1_autosave.bundle");
        Assert.NotNull(savedBase64);
        var savedBytes = Convert.FromBase64String(savedBase64);
        Assert.True(savedBytes.Length > 0, "Saved file is empty");

        var reloaded = BundleReader.Read(savedBytes, "_wd2_saveslot1_autosave.bundle");
        Assert.NotNull(reloaded.Metadata);

        var original = BundleReader.Read(TestDataHelper.GetPath("S2", "_wd2_saveslot1_autosave.bundle"));
        Assert.Equal(original.Files.Count, reloaded.Files.Count);
        for (var i = 1; i < original.Files.Count; i++)
        {
            Assert.Equal(original.Files[i].NameSymbol, reloaded.Files[i].NameSymbol);
            Assert.True(original.Files[i].Data.AsSpan().SequenceEqual(reloaded.Files[i].Data),
                $"Inner file {i} changed while editing the metadata");
        }

        var slotBase64 = await FakeSaveDirectory.ReadFileAsync(page, "wd2_saveslot1.bundle");
        Assert.NotNull(slotBase64);
        var slotBytes = Convert.FromBase64String(slotBase64);
        var slotReloaded = BundleReader.Read(slotBytes, "wd2_saveslot1.bundle");
        Assert.NotNull(slotReloaded.Metadata);

        var slotEpProp = slotReloaded.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0xB218E7C003A67CE9);
        Assert.NotNull(slotEpProp);
        var slotEpValue = ((TwdSaveEditor.Core.Model.StringValue)slotEpProp.Value).Value;
        Assert.Equal("WalkingDead202", slotEpValue);

        var slotProgressProp = slotReloaded.Metadata.AllProperties
            .FirstOrDefault(p => p.KeySymbol.Value == 0x94C245DACB1ADDC3);
        Assert.NotNull(slotProgressProp);
        Assert.Equal(2, ((TwdSaveEditor.Core.Model.IntValue)slotProgressProp.Value).Value);

        var reinjectBase64 = Convert.ToBase64String(savedBytes);
        await FakeSaveDirectory.InstallAsync(page, new Dictionary<string, string>
        {
            ["_wd2_saveslot1_autosave.bundle"] = reinjectBase64,
        });

        await openDirBtn.ClickAsync();
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        await saveItems.First.ClickAsync();
        await page.WaitForTimeoutAsync(1000);

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

        var openDirBtn = page.Locator("[data-testid='open-directory']");
        await openDirBtn.ClickAsync();

        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        await saveItems.First.ClickAsync();

        var decisionEditor = page.Locator(".decision-editor");
        await decisionEditor.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        await Assertions.Expect(decisionEditor).ToBeVisibleAsync();
    }
}
