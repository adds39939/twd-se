using Microsoft.Playwright;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.EventLog;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Hashing;
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

    private static readonly string[] MichonneFiles =
    [
        "wdm_saveslot2.bundle", "_wdm_saveslot2_autosave.bundle", "_wdm_saveslot2_checkpoint1.bundle", "_wdm_saveslot2_id.estore",
        "_wdm_saveslot2_id_Page969.epage", "_wdm_saveslot2_id_Page1963.epage",
    ];

    private static readonly string[] S4Files =
    [
        "wd4_saveslot2.bundle", "_wd4_saveslot2_autosave.bundle", "_wd4_saveslot2_id.estore",
        "_wd4_saveslot2_id_Page734.epage", "_wd4_saveslot2_id_Page17124.epage", "_wd4_saveslot2_id_Page18120.epage", "_wd4_saveslot2_id_Page19079.epage",
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
    public async Task Tabs_KeepTheirStateAndFollowUnsavedChanges()
    {
        var page = await _fixture.NewPage();
        await InjectSaveFiles(page, "S2", S2Files);

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();
        await page.Locator("[data-testid='presets'] summary").ClickAsync();
        await page.Locator("[data-testid='presets-endings']").CheckAsync();

        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes");
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='restart-episode']")).ToHaveValueAsync("1");
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("2");
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync("LodgeRear");
        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("checkpoint Lodge Rear");

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='inventory-summary']")).ToContainTextAsync("Episode 2: A House Divided, 0 items");
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='restart-episode']")).ToHaveValueAsync("2");
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter']")).ToHaveValueAsync("LodgeRear");

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='inventory-summary']")).ToContainTextAsync("Episode 2: A House Divided, 0 items");
        await Assertions.Expect(page.Locator("[data-testid='inventory-carried']")).ToBeVisibleAsync();

        await page.Locator("[data-testid='tab-decisions']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='presets-endings']")).ToBeCheckedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Ending: Wellington" })).ToBeVisibleAsync();
        var later = page.Locator(".choice-row", new() { HasTextString = "Who did you sit with at dinner?" }).First.Locator("select");
        await Assertions.Expect(later).ToHaveValueAsync("-1");
    }

    [Fact]
    public async Task S2Inventory_GivesAChapterTheItemsPickedUpEarlier()
    {
        const string checkpointName = "_wd2_saveslot1_checkpoint1.bundle";

        var page = await _fixture.NewPage();
        await InjectSaveFiles(page, "S2", S2Files);

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("2");
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync("LodgeRear");

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();
        var summary = page.Locator("[data-testid='inventory-summary']");
        var held = page.Locator(".inventory-item input:checked");
        await Assertions.Expect(summary).ToContainTextAsync("Clementine");
        await Assertions.Expect(summary).ToContainTextAsync("Episode 2: A House Divided, 0 items");
        await Assertions.Expect(page.Locator(".inventory-item")).ToHaveCountAsync(6);
        await Assertions.Expect(held).ToHaveCountAsync(0);

        await page.Locator("[data-testid='inventory-carried']").ClickAsync();
        await Assertions.Expect(held).ToHaveCountAsync(5);
        await Assertions.Expect(page.Locator("[data-testid='inventory-item-ui_item_hammer']")).Not.ToBeCheckedAsync();

        await page.Locator("[data-testid='inventory-item-ui_item_binoculars']").UncheckAsync();
        await page.Locator("[data-testid='inventory-item-ui_item_hammer']").CheckAsync();
        await Assertions.Expect(summary).ToContainTextAsync("5 items");

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = "Saved wd2_saveslot1.bundle" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        var checkpointBase64 = await FakeSaveDirectory.ReadFileAsync(page, checkpointName);
        Assert.NotNull(checkpointBase64);
        var checkpoint = BundleReader.Read(Convert.FromBase64String(checkpointBase64), checkpointName);
        var inventory = checkpoint.FindFile(TelltaleHash.ComputeCrc64("\"logic_inventory:logic.scene\" Runtime Properties"));
        Assert.NotNull(inventory);
        Assert.True(BundleReader.TryParseProperties(inventory));
        Assert.Equal(
            ["ui_item_bottleWater", "ui_item_lighter", "ui_item_watch", "ui_item_knifeSurvival", "ui_item_hammer"],
            inventory.Properties!.GetStrings("Items - Clementine"));
    }

    [Fact]
    public async Task S1Inventory_WritesCountedItemsIntoAChapterCheckpoint()
    {
        const string autosaveName = "_wd1_saveslot2_autosave.bundle";

        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S1", "wd1_saveslot2.bundle");

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("1");
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync("OnDrugstoreExterior");

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();
        var summary = page.Locator("[data-testid='inventory-summary']");
        await Assertions.Expect(summary).ToContainTextAsync("Lee");
        await Assertions.Expect(summary).ToContainTextAsync("Episode 1: A New Day, 0 items");
        await Assertions.Expect(page.Locator(".inventory-item")).ToHaveCountAsync(17);

        await page.Locator("[data-testid='inventory-carried']").ClickAsync();
        await Assertions.Expect(page.Locator(".inventory-item input:checked")).ToHaveCountAsync(3);
        await Assertions.Expect(page.Locator("[data-testid='inventory-item-Office - got photo']")).ToBeCheckedAsync();

        await page.Locator("[data-testid='inventory-item-Drugstore - PlayerFood']").CheckAsync();
        var food = page.Locator("[data-testid='inventory-count-Drugstore - PlayerFood']");
        await Assertions.Expect(food).ToHaveValueAsync("1");
        await Assertions.Expect(food.Locator("option")).ToHaveCountAsync(4);
        await food.SelectOptionAsync("3");
        await page.Locator("[data-testid='inventory-item-Inventory - Remote Control']").UncheckAsync();
        await Assertions.Expect(summary).ToContainTextAsync("3 items");

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = "Saved wd1_saveslot2.bundle" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        var autosaveBase64 = await FakeSaveDirectory.ReadFileAsync(page, autosaveName);
        Assert.NotNull(autosaveBase64);
        var autosave = BundleReader.Read(Convert.FromBase64String(autosaveBase64), autosaveName);
        var game = autosave.FindFile(TelltaleHash.ComputeCrc64("\"logic_game:module_logic.scene\" Runtime Properties"));
        var inventory = autosave.FindFile(TelltaleHash.ComputeCrc64("\"logic_inventory_items:module_logic.scene\" Runtime Properties"));
        Assert.NotNull(game);
        Assert.NotNull(inventory);
        Assert.True(BundleReader.TryParseProperties(game));
        Assert.True(BundleReader.TryParseProperties(inventory));
        Assert.Equal(3, game.Properties!.GetInt("Drugstore - PlayerFood"));
        Assert.True(game.Properties.GetBool("Office - got photo"));
        Assert.Equal(1, inventory.Properties!.GetInt("Inventory - Bandage"));
        Assert.NotEqual(1, inventory.Properties.GetInt("Inventory - Remote Control"));
    }

    [Fact]
    public async Task S3DecisionChapterAndItems_AreWrittenToTheLogAndANewSave()
    {
        const string autosaveName = "_wd3_saveslot1_autosave.bundle";

        var page = await _fixture.NewPage();
        await InjectSaveFile(page, "S3", "wd3_saveslot1.bundle");

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();

        await page.GetByText("Carried over from the previous season").ClickAsync();
        var ending = page.Locator(".choice-row", new() { HasTextString = "Ending Choice?" }).First.Locator("select");
        await Assertions.Expect(ending).ToHaveValueAsync("0");
        await ending.SelectOptionAsync(new SelectOptionValue { Label = "Kenny" });

        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter'] option")).ToHaveCountAsync(17);
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync("VirginiaRoadTruck");
        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("checkpoint Virginia Road - Truck");

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();
        var summary = page.Locator("[data-testid='inventory-summary']");
        var held = page.Locator(".inventory-item input:checked");
        await Assertions.Expect(summary).ToContainTextAsync("Javier");
        await Assertions.Expect(summary).ToContainTextAsync("Episode 1: Ties That Bind - Part One, 0 items");
        await Assertions.Expect(page.Locator(".inventory-item")).ToHaveCountAsync(4);
        await Assertions.Expect(held).ToHaveCountAsync(0);

        await page.Locator("[data-testid='inventory-carried']").ClickAsync();
        await Assertions.Expect(held).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-testid='inventory-item-Inventory - Candy Bar']")).ToBeCheckedAsync();
        await page.Locator("[data-testid='inventory-item-Inventory - Crowbar']").CheckAsync();
        await Assertions.Expect(summary).ToContainTextAsync("2 items");

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = "Saved wd3_saveslot1.bundle" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        var autosaveBase64 = await FakeSaveDirectory.ReadFileAsync(page, autosaveName);
        Assert.NotNull(autosaveBase64);
        var autosave = BundleReader.Read(Convert.FromBase64String(autosaveBase64), autosaveName);
        Assert.Equal(1, autosave.Metadata!.GetInt(SaveMetadataKeys.Episode));
        Assert.Equal(22, autosave.Metadata.GetInt(SaveMetadataKeys.Serial));
        var game = autosave.FindFile(TelltaleHash.ComputeCrc64("\"logic_game:logic.scene\" Runtime Properties"));
        Assert.NotNull(game);
        Assert.True(BundleReader.TryParseProperties(game));
        Assert.Equal("Kenny", game.Properties!.GetString("Episode 205 - Ending Choice"));
        Assert.True(game.Properties.GetBool("bEnteredJunkyardHill"));

        foreach (var agent in new[] { "logic_inventory", "logic_inventory_Javier" })
        {
            var inventory = autosave.FindFile(TelltaleHash.ComputeCrc64($"\"{agent}:logic.scene\" Runtime Properties"));
            Assert.NotNull(inventory);
            Assert.True(BundleReader.TryParseProperties(inventory));
            Assert.Equal(1, inventory.Properties!.GetInt("Inventory - Candy Bar"));
            Assert.Equal(1, inventory.Properties.GetInt("Inventory - Crowbar"));
            Assert.NotEqual(1, inventory.Properties.GetInt("Inventory - Siphon"));
        }

        var storageBase64 = await FakeSaveDirectory.ReadFileAsync(page, "_wd3_saveslot1_id.estore");
        Assert.NotNull(storageBase64);
        Assert.Equal(22, EventLogCodec.ReadStorage(Convert.FromBase64String(storageBase64)).CurrentPage!.Events.Last().SaveSerial);

        var slotBase64 = await FakeSaveDirectory.ReadFileAsync(page, "wd3_saveslot1.bundle");
        Assert.NotNull(slotBase64);
        var slot = BundleReader.Read(Convert.FromBase64String(slotBase64), "wd3_saveslot1.bundle");
        Assert.Equal(autosaveName, slot.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(22, slot.Metadata.GetInt(SlotMetadataKeys.LatestSerial));
    }

    [Fact]
    public async Task MichonneDecisionChapterAndItems_AreWrittenToTheLogAndACheckpoint()
    {
        const string checkpointName = "_wdm_saveslot2_checkpoint1.bundle";

        var page = await _fixture.NewPage();
        await InjectSaveFiles(page, "Michonne", MichonneFiles);

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();

        var endIt = page.Locator(".choice-row", new() { HasTextString = "Did you try to end it?" }).First.Locator("select");
        await endIt.SelectOptionAsync(new SelectOptionValue { Label = "Pulled the trigger" });

        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("checkpoint Flagship Interior Escape");
        await Assertions.Expect(page.Locator("[data-testid='restart-episode'] option")).ToHaveCountAsync(3);
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter'] option")).ToHaveCountAsync(21);
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync("FerryInteriorSnackBar");
        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("checkpoint Ferry Interior - Snack Bar");

        await page.Locator("[data-testid='tab-inventory']").ClickAsync();
        var held = page.Locator(".inventory-item input:checked");
        await Assertions.Expect(page.Locator("[data-testid='inventory-summary']")).ToContainTextAsync("Michonne");
        await Assertions.Expect(page.Locator(".inventory-item")).ToHaveCountAsync(5);
        await Assertions.Expect(held).ToHaveCountAsync(0);
        await page.Locator("[data-testid='inventory-carried']").ClickAsync();
        await Assertions.Expect(held).ToHaveCountAsync(3);
        await Assertions.Expect(page.Locator("[data-testid='inventory-item-Inventory - Machete']")).ToBeCheckedAsync();

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = "Saved wdm_saveslot2.bundle" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        var checkpointBase64 = await FakeSaveDirectory.ReadFileAsync(page, checkpointName);
        Assert.NotNull(checkpointBase64);
        var checkpoint = BundleReader.Read(Convert.FromBase64String(checkpointBase64), checkpointName);
        Assert.Equal(1, checkpoint.Metadata!.GetInt(SaveMetadataKeys.Episode));
        Assert.Equal(24, checkpoint.Metadata.GetInt(SaveMetadataKeys.Serial));
        Assert.Equal("101_chapter4", checkpoint.Metadata.GetString(SaveMetadataKeys.ChapterId));
        var game = checkpoint.FindFile(TelltaleHash.ComputeCrc64("\"logic_game:logic.scene\" Runtime Properties"));
        Assert.NotNull(game);
        Assert.True(BundleReader.TryParseProperties(game));
        Assert.True(game.Properties!.GetBool("2FerryInterior - In Snack Bar"));
        var inventory = checkpoint.FindFile(TelltaleHash.ComputeCrc64("\"logic_inventory:logic.scene\" Runtime Properties"));
        Assert.NotNull(inventory);
        Assert.True(BundleReader.TryParseProperties(inventory));
        Assert.Equal(1, inventory.Properties!.GetInt("Inventory - Machete"));
        Assert.Equal(1, inventory.Properties.GetInt("Inventory - Flashlight"));
        Assert.NotEqual(1, inventory.Properties.GetInt("Inventory - Screwdriver"));
        Assert.Null(await FakeSaveDirectory.ReadFileAsync(page, "_wdm_saveslot2_autosave.bundle"));

        var storageBase64 = await FakeSaveDirectory.ReadFileAsync(page, "_wdm_saveslot2_id.estore");
        Assert.NotNull(storageBase64);
        Assert.Equal(24, EventLogCodec.ReadStorage(Convert.FromBase64String(storageBase64)).CurrentPage!.Events.Last().SaveSerial);

        var slotBase64 = await FakeSaveDirectory.ReadFileAsync(page, "wdm_saveslot2.bundle");
        Assert.NotNull(slotBase64);
        var slot = BundleReader.Read(Convert.FromBase64String(slotBase64), "wdm_saveslot2.bundle");
        Assert.Equal(checkpointName, slot.Metadata!.GetString(SlotMetadataKeys.LatestSave));
        Assert.Equal(24, slot.Metadata.GetInt(SlotMetadataKeys.LatestSerial));
    }

    [Fact]
    public async Task S4PresetDecisionAndChapter_AreWrittenToTheLogAndTheSave()
    {
        const string autosaveName = "_wd4_saveslot2_autosave.bundle";

        var page = await _fixture.NewPage();
        await InjectSaveFiles(page, "S4", S4Files);

        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await page.Locator(".save-item").First.ClickAsync();

        await Assertions.Expect(page.Locator("[data-testid='season-s4'] > summary .badge")).ToContainTextAsync("66 choices");
        await page.GetByText("Carried over from the previous season").ClickAsync();
        var ending = page.Locator(".choice-row", new() { HasTextString = "Ending Choice?" }).First.Locator("select");
        await Assertions.Expect(ending).ToHaveValueAsync("0");
        await ending.SelectOptionAsync(new SelectOptionValue { Label = "Kenny" });
        await page.Locator("[data-testid='presets'] summary").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save Violet Path" }).ClickAsync();

        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("checkpoint Boarding School Interior");
        await Assertions.Expect(page.Locator("[data-testid='restart-episode'] option")).ToHaveCountAsync(4);
        await page.Locator("[data-testid='restart-episode']").SelectOptionAsync("3");
        await page.Locator("[data-testid='restart-chapter']").SelectOptionAsync("ForestCamp");
        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("Episode 3: Broken Toys — checkpoint Forest Camp");

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = "Saved wd4_saveslot2.bundle" }).First)
            .ToBeVisibleAsync(new() { Timeout = 20000 });
        await Assertions.Expect(page.Locator(".toast-error")).ToHaveCountAsync(0);

        var autosaveBase64 = await FakeSaveDirectory.ReadFileAsync(page, autosaveName);
        Assert.NotNull(autosaveBase64);
        var autosave = BundleReader.Read(Convert.FromBase64String(autosaveBase64), autosaveName);
        Assert.Equal(3, autosave.Metadata!.GetInt(SaveMetadataKeys.Episode));
        var game = autosave.FindFile(TelltaleHash.ComputeCrc64("\"logic_game:logic.scene\" Runtime Properties"));
        Assert.NotNull(game);
        Assert.True(BundleReader.TryParseProperties(game));
        Assert.Equal("Kenny", game.Properties!.GetString("Episode 205 - Ending Choice"));
        var systems = autosave.FindFile(TelltaleHash.ComputeCrc64("\"logic_systems:logic.scene\" Runtime Properties"));
        Assert.NotNull(systems);
        Assert.True(BundleReader.TryParseProperties(systems));
        Assert.Equal("DebugMenu", systems.Properties!.GetString("Script - Previous"));

        var slotBase64 = await FakeSaveDirectory.ReadFileAsync(page, "wd4_saveslot2.bundle");
        Assert.NotNull(slotBase64);
        var slot = BundleReader.Read(Convert.FromBase64String(slotBase64), "wd4_saveslot2.bundle");
        Assert.Equal(3, slot.Metadata!.GetInt(SlotMetadataKeys.EpisodeInProgress));
        Assert.Equal(2, slot.Metadata.GetInt("Last Episode Finished"));
        Assert.Equal(autosaveName, slot.Metadata.GetString(SlotMetadataKeys.LatestSave));
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
