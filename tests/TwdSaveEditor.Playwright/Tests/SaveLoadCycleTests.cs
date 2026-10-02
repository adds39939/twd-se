using Microsoft.Playwright;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
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

    private static readonly string[] S2Files =
    [
        "wd2_saveslot1.bundle", "_wd2_saveslot1_autosave.bundle", "_wd2_saveslot1_id.estore",
        "_wd2_saveslot1_id_Page913.epage", "_wd2_saveslot1_id_Page1897.epage", "_wd2_saveslot1_id_Page2734.epage",
    ];

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
    public async Task S2SaveSet_IsListedAsOneSave()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFiles(page, "S2", S2Files);

        await page.Locator("[data-testid='open-directory']").ClickAsync();

        var saveItems = page.Locator(".save-item");
        await Assertions.Expect(saveItems).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        await saveItems.First.ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='app-ready']")).ToBeVisibleAsync();
        Assert.True(await page.Locator(".tab-bar button").CountAsync() >= 3);
    }

    [Fact]
    public async Task S2RestartFromNextEpisode_RewindsTheSlotAndFillsTheEventLog()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFiles(page, "S2", S2Files);

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("2");
        await page.Locator("[data-testid='restart-button']").ClickAsync();

        var state = page.Locator("[data-testid='resume-state']");
        await Assertions.Expect(state).ToContainTextAsync("Episode 2: A House Divided");
        await Assertions.Expect(state).ToContainTextAsync("from the beginning");

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = "Saved wd2_saveslot1.bundle" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        var slotBase64 = await FakeSaveDirectory.ReadFileAsync(page, "wd2_saveslot1.bundle");
        Assert.NotNull(slotBase64);
        var slot = BundleReader.Read(Convert.FromBase64String(slotBase64), "wd2_saveslot1.bundle");
        Assert.Equal("WalkingDead202", slot.Metadata!.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal("_wd2_saveslot1_autosave.bundle", slot.Metadata.GetString(SlotMetadataKeys.LatestSave));

        var storageBase64 = await FakeSaveDirectory.ReadFileAsync(page, "_wd2_saveslot1_id.estore");
        Assert.NotNull(storageBase64);
        var storage = EventLogCodec.ReadStorage(Convert.FromBase64String(storageBase64));
        Assert.Equal(3, storage.Pages.Count);
        Assert.Equal(16, storage.CurrentPage!.Events.Last().SaveSerial);
        Assert.Equal(355, storage.CurrentPage.Events.Count);

        var original = TestDataHelper.GetPath("S2", "_wd2_saveslot1_autosave.bundle");
        var autosaveBase64 = await FakeSaveDirectory.ReadFileAsync(page, "_wd2_saveslot1_autosave.bundle");
        Assert.Equal(Convert.ToBase64String(File.ReadAllBytes(original)), autosaveBase64);
    }

    [Fact]
    public async Task S2ResumeFromChapter_WritesACheckpointForThatScene()
    {
        const string checkpointName = "_wd2_saveslot1_checkpoint1.bundle";

        var page = await _fixture.NewPage();
        await InjectSaveFiles(page, "S2", S2Files);

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("2");
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter'] option")).ToHaveCountAsync(26);
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync("LodgeMainDinner");
        await page.Locator("[data-testid='restart-button']").ClickAsync();

        var state = page.Locator("[data-testid='resume-state']");
        await Assertions.Expect(state).ToContainTextAsync("Episode 2: A House Divided");
        await Assertions.Expect(state).ToContainTextAsync("checkpoint Lodge Main Dinner");

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = "Saved wd2_saveslot1.bundle" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        var slotBase64 = await FakeSaveDirectory.ReadFileAsync(page, "wd2_saveslot1.bundle");
        Assert.NotNull(slotBase64);
        var slot = BundleReader.Read(Convert.FromBase64String(slotBase64), "wd2_saveslot1.bundle");
        Assert.Equal("WalkingDead202", slot.Metadata!.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(checkpointName, slot.Metadata.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(17, slot.Metadata.GetInt(SlotMetadataKeys.LatestSerial));

        var checkpointBase64 = await FakeSaveDirectory.ReadFileAsync(page, checkpointName);
        Assert.NotNull(checkpointBase64);
        var checkpoint = BundleReader.Read(Convert.FromBase64String(checkpointBase64), checkpointName);
        Assert.Equal("WalkingDead202", checkpoint.Metadata!.GetString(SaveMetadataKeys.Episode));
        Assert.Equal("202_chapter9", checkpoint.Metadata.GetString(SaveMetadataKeys.ChapterId));
        Assert.Equal(17, checkpoint.Metadata.GetInt(SaveMetadataKeys.Serial));

        var storageBase64 = await FakeSaveDirectory.ReadFileAsync(page, "_wd2_saveslot1_id.estore");
        Assert.NotNull(storageBase64);
        Assert.Equal(17, EventLogCodec.ReadStorage(Convert.FromBase64String(storageBase64)).CurrentPage!.Events.Last().SaveSerial);
    }

    [Fact]
    public async Task S2NewSave_IsWrittenWithACheckpointAndAnEventLog()
    {
        const string checkpointName = "_wd2_saveslot1_checkpoint1.bundle";

        var page = await _fixture.NewPage();
        await FakeSaveDirectory.InstallAsync(page, new Dictionary<string, string>());
        await page.Locator("[data-testid='open-directory']").ClickAsync();

        await page.Locator("[data-testid='new-save-btn']").ClickAsync();
        var dialog = page.Locator("[data-testid='new-save-dialog']");
        await dialog.Locator("select").First.SelectOptionAsync("s2");
        await dialog.Locator("select").Nth(1).SelectOptionAsync("2");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(1, new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        var slotBase64 = await FakeSaveDirectory.ReadFileAsync(page, "wd2_saveslot1.bundle");
        Assert.NotNull(slotBase64);
        var slot = BundleReader.Read(Convert.FromBase64String(slotBase64), "wd2_saveslot1.bundle");
        Assert.Equal("WalkingDead202", slot.Metadata!.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(checkpointName, slot.Metadata.GetString(SlotMetadataKeys.LatestSave));

        var checkpointBase64 = await FakeSaveDirectory.ReadFileAsync(page, checkpointName);
        Assert.NotNull(checkpointBase64);
        var checkpoint = BundleReader.Read(Convert.FromBase64String(checkpointBase64), checkpointName);
        Assert.Equal("WalkingDead202", checkpoint.Metadata!.GetString(SaveMetadataKeys.Episode));

        var storageBase64 = await FakeSaveDirectory.ReadFileAsync(page, "_wd2_saveslot1_id.estore");
        Assert.NotNull(storageBase64);
        var events = EventLogCodec.ReadStorage(Convert.FromBase64String(storageBase64)).CurrentPage!.Events;
        Assert.Equal(1, events.Last().SaveSerial);
        Assert.True(events.Count > 1);

        await page.Locator(".save-item").First.ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        var state = page.Locator("[data-testid='resume-state']");
        await Assertions.Expect(state).ToContainTextAsync("Episode 2: A House Divided");
        await Assertions.Expect(state).ToContainTextAsync("from the beginning");
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
