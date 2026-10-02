using Microsoft.Playwright;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class Season1Tests
{
    private const string Slot = "wd1_saveslot2.bundle";
    private const string Autosave = "_wd1_saveslot2_autosave.bundle";
    private const ulong LogicGameProperties = 0x1D3802238E8CE045;

    private readonly PlaywrightFixture _fixture;

    public Season1Tests(PlaywrightFixture fixture) => _fixture = fixture;

    private async Task<IPage> OpenSeason1Save()
    {
        var page = await _fixture.NewPage();
        var directory = new Dictionary<string, string>();
        await FakeSaveDirectory.AddSaveAsync(directory, "S1", Slot);
        await FakeSaveDirectory.InstallAsync(page, directory);

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        var saveItems = page.Locator(".save-item");
        await saveItems.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        await saveItems.First.ClickAsync();
        await page.Locator(".decision-editor").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        return page;
    }

    private static async Task SaveChanges(IPage page, string fileName)
    {
        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = $"Saved {fileName}" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);
    }

    private static async Task<SaveSlot> ReadSavedBundle(IPage page, string fileName)
    {
        var base64 = await FakeSaveDirectory.ReadFileAsync(page, fileName);
        Assert.NotNull(base64);
        return BundleReader.Read(Convert.FromBase64String(base64), fileName);
    }

    [Fact]
    public async Task Autosave_IsListedAsPartOfItsSlot()
    {
        var page = await OpenSeason1Save();

        var saveItems = page.Locator(".save-item");
        await Assertions.Expect(saveItems).ToHaveCountAsync(1);
        await Assertions.Expect(saveItems.First).ToContainTextAsync(Slot);
    }

    [Fact]
    public async Task ChoiceEdit_IsWrittenToTheSlotAndTheAutosave()
    {
        var page = await OpenSeason1Save();

        var choice = page.Locator(".choice-row", new() { HasTextString = "Who Lee saved in the drugstore" });
        await Assertions.Expect(choice.Locator("select")).ToHaveValueAsync("0");
        await choice.Locator("select").SelectOptionAsync(new SelectOptionValue { Label = "Saved Doug" });

        await SaveChanges(page, Slot);

        var slot = await ReadSavedBundle(page, Slot);
        Assert.Equal("doug", slot.Metadata!.GetString("Persistent - 101 - DougCarley Saved"));

        var autosave = await ReadSavedBundle(page, Autosave);
        var logic = autosave.FindFile(LogicGameProperties)!;
        Assert.True(BundleReader.TryParseProperties(logic));
        Assert.Equal("doug", logic.Properties!.GetString("DougCarley Saved"));
        Assert.Equal(11672, autosave.Files.Count);

        var backup = await FakeSaveDirectory.GetLastBackupFilesAsync(page);
        Assert.NotNull(backup);
        Assert.Contains(Slot, backup);
        Assert.Contains(Autosave, backup);
    }

    [Fact]
    public async Task RestartEpisode_RemovesTheCheckpointAndRewindsTheSlot()
    {
        var page = await OpenSeason1Save();

        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("2");
        await page.Locator("[data-testid='restart-button']").ClickAsync();

        var state = page.Locator("[data-testid='resume-state']");
        await Assertions.Expect(state).ToContainTextAsync("Episode 2: Starved for Help");
        await Assertions.Expect(state).ToContainTextAsync("from the beginning");

        await SaveChanges(page, Slot);

        var files = await FakeSaveDirectory.GetFileNamesAsync(page);
        Assert.Contains(Slot, files);
        Assert.DoesNotContain(Autosave, files);

        var slot = await ReadSavedBundle(page, Slot);
        Assert.Equal(2, slot.Metadata!.GetInt(SlotMetadataKeys.Progress));
        Assert.Equal("WalkingDead102", slot.Metadata.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(string.Empty, slot.Metadata.GetString(SlotMetadataKeys.LatestSave));
        Assert.True(slot.Metadata.GetBool(SlotMetadataKeys.CompletedEpisode(1)));
        Assert.False(slot.Metadata.GetBool(SlotMetadataKeys.CompletedEpisode(2)));

        var backup = await FakeSaveDirectory.GetLastBackupFilesAsync(page);
        Assert.NotNull(backup);
        Assert.Contains(Autosave, backup);
    }

    [Fact]
    public async Task ResumeFromChapter_WritesASmallCheckpointForThatChapter()
    {
        var page = await OpenSeason1Save();

        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("5");
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter'] option")).ToHaveCountAsync(18);
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync("OnJewelryStore");
        await page.Locator("[data-testid='restart-button']").ClickAsync();

        var state = page.Locator("[data-testid='resume-state']");
        await Assertions.Expect(state).ToContainTextAsync("Episode 5: No Time Left");
        await Assertions.Expect(state).ToContainTextAsync("checkpoint Jewelry Store");

        await SaveChanges(page, Slot);

        var slot = await ReadSavedBundle(page, Slot);
        Assert.Equal(5, slot.Metadata!.GetInt(SlotMetadataKeys.Progress));
        Assert.Equal(Autosave, slot.Metadata.GetString(SlotMetadataKeys.LatestSave));

        var autosave = await ReadSavedBundle(page, Autosave);
        Assert.Equal(6, autosave.Files.Count);
        Assert.Equal("WalkingDead105", autosave.Metadata!.GetString(SaveMetadataKeys.Episode));
        Assert.Equal("OnJewelryStore", autosave.Metadata.GetString(SaveMetadataKeys.ChapterId));
    }

    [Fact]
    public async Task NewSave_StartsAtTheChosenEpisodeWithEarlierDecisionsSet()
    {
        var page = await _fixture.NewPage();
        await FakeSaveDirectory.InstallAsync(page, new Dictionary<string, string>());
        await page.Locator("[data-testid='open-directory']").ClickAsync();

        await page.Locator("[data-testid='new-save-btn']").ClickAsync();
        var dialog = page.Locator("[data-testid='new-save-dialog']");
        await dialog.Locator("select").First.SelectOptionAsync("s1");
        await dialog.Locator("select").Nth(1).SelectOptionAsync("3");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        var saveItems = page.Locator(".save-item");
        await Assertions.Expect(saveItems).ToHaveCountAsync(1, new() { Timeout = 10000 });

        var slot = await ReadSavedBundle(page, "wd1_saveslot1.bundle");
        Assert.Equal(3, slot.Metadata!.GetInt(SlotMetadataKeys.Progress));
        Assert.Equal("WalkingDead103", slot.Metadata.GetString(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(string.Empty, slot.Metadata.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal("carley", slot.Metadata.GetString("Persistent - 101 - DougCarley Saved"));
        Assert.Equal("true", slot.Metadata.GetString("Persistent - 102 - Chopped Leg"));
        Assert.Null(slot.Metadata.GetString("Persistent - 103 - Left Lilly"));

        await saveItems.First.ClickAsync();
        var choice = page.Locator(".choice-row", new() { HasTextString = "Who Lee saved in the drugstore" });
        await Assertions.Expect(choice.Locator("select")).ToHaveValueAsync("0");
    }
}
