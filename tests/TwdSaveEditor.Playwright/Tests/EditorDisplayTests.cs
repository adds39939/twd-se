using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class EditorDisplayTests
{
    private readonly PlaywrightFixture _fixture;

    public EditorDisplayTests(PlaywrightFixture fixture) => _fixture = fixture;

    private static ILocator SaveItem(IPage page, string fileName) =>
        page.Locator(".save-item").Filter(new LocatorFilterOptions { HasText = fileName });

    private static ILocator Decision(IPage page, string description) =>
        page.Locator(".choice-row", new() { HasTextString = description }).First.Locator("select");

    private async Task<IPage> Open(params (string Season, string FileName, string? InjectName)[] saves)
    {
        var page = await _fixture.NewPage();
        var directory = new Dictionary<string, string>();
        foreach (var (season, fileName, injectName) in saves)
        {
            await FakeSaveDirectory.AddSaveAsync(directory, season, fileName, injectName);
        }

        await FakeSaveDirectory.InstallAsync(page, directory);
        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(saves.Count(save => !save.FileName.StartsWith('_')));
        return page;
    }

    [Fact]
    public async Task NotSet_ClearsADecisionAndTheSaveKeepsItCleared()
    {
        var page = await Open(("S1", "wd1_saveslot2.bundle", null));
        await SaveItem(page, "wd1_saveslot2.bundle").ClickAsync();

        var row = page.Locator("[data-testid='season-s1'] details[open] .choice-row").First;
        var description = await row.Locator(".choice-desc").TextContentAsync();
        var choice = row.Locator("select");
        await Assertions.Expect(choice).Not.ToHaveValueAsync("-1");
        await Assertions.Expect(choice.Locator("option[value='-1']")).ToBeEnabledAsync();

        await choice.SelectOptionAsync("-1");
        await Assertions.Expect(choice).ToHaveValueAsync("-1");
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".header-status")).ToContainTextAsync("Saved wd1_saveslot2.bundle");
        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveCountAsync(0);
        await SaveItem(page, "wd1_saveslot2.bundle").ClickAsync();

        await Assertions.Expect(Decision(page, description!)).ToHaveValueAsync("-1");
    }

    [Fact]
    public async Task ImportButton_ImportsTheShownSaveOnTheFirstClick()
    {
        var page = await Open(("S1", "wd1_saveslot2.bundle", null), ("S2", "wd2_saveslot1.bundle", null));
        await SaveItem(page, "wd2_saveslot1.bundle").ClickAsync();

        await Assertions.Expect(page.Locator(".import-controls select")).ToHaveValueAsync("wd1_saveslot2.bundle");
        await page.Locator(".import-controls button").ClickAsync();

        await Assertions.Expect(SaveItem(page, "wd2_saveslot1.bundle").Locator("[data-testid='save-unsaved']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Preset_ShowsTheDecisionsItSets()
    {
        var page = await Open(("S2", "wd2_saveslot1.bundle", null));
        await SaveItem(page, "wd2_saveslot1.bundle").ClickAsync();
        var dinner = Decision(page, "Who did you sit with at dinner?");
        await Assertions.Expect(dinner).ToHaveValueAsync("-1");

        await page.Locator("[data-testid='presets'] summary").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Side with Kenny" }).ClickAsync();

        await Assertions.Expect(dinner.Locator("option:checked")).ToHaveTextAsync("Sat with Kenny");
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");
    }

    [Fact]
    public async Task UnreadableDialogLog_ExplainsWhyTheSaveCannotBeEditedAndIsLeftAlone()
    {
        const string storage = "_wd3_saveslot1_id.estore";
        var page = await _fixture.NewPage();
        var directory = new Dictionary<string, string>();
        await FakeSaveDirectory.AddSaveAsync(directory, "S3", "wd3_saveslot1.bundle");
        directory[storage] = Convert.ToBase64String(Convert.FromBase64String(directory[storage])[..100]);
        await FakeSaveDirectory.InstallAsync(page, directory);
        await page.Locator("[data-testid='open-directory']").ClickAsync();
        await SaveItem(page, "wd3_saveslot1.bundle").ClickAsync();

        await Assertions.Expect(page.Locator(".decision-editor [data-testid='log-damaged']")).ToBeVisibleAsync();
        await Assertions.Expect(SaveItem(page, "wd3_saveslot1.bundle").Locator("[data-testid='save-damaged']")).ToBeVisibleAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid='restart-episode']")).ToBeDisabledAsync();
        await Assertions.Expect(page.Locator("[data-testid='restart-chapter']")).ToBeDisabledAsync();
        await page.Locator("[data-testid='tab-inventory']").ClickAsync();
        await Assertions.Expect(page.Locator(".tab-panel:not([hidden])")).ToContainTextAsync("dialog log of this save cannot be read");

        await page.Locator(".save-btn").ClickAsync();
        await Assertions.Expect(page.Locator(".header-status")).ToContainTextAsync("Saved wd3_saveslot1.bundle");
        Assert.Equal(directory[storage], await FakeSaveDirectory.ReadFileAsync(page, storage));
    }

    [Fact]
    public async Task SaveList_DescribesEachSave()
    {
        var page = await Open(("S1", "wd1_saveslot2.bundle", null), ("S3", "wd3_saveslot1.bundle", null));

        var season1 = SaveItem(page, "wd1_saveslot2.bundle");
        await Assertions.Expect(season1.Locator("[data-testid='save-title']")).ToHaveTextAsync("Season 1 · Slot 2");
        await Assertions.Expect(season1.Locator("[data-testid='save-position']")).ToHaveTextAsync("Episode 4: Around Every Corner · Missing Clementine");
        var season3 = SaveItem(page, "wd3_saveslot1.bundle");
        await Assertions.Expect(season3.Locator("[data-testid='save-title']")).ToHaveTextAsync("A New Frontier (Season 3) · Slot 1");
        await Assertions.Expect(season3.Locator("[data-testid='save-position']")).ToHaveTextAsync("Episode 1: Ties That Bind - Part One · Junkyard Day");
        await Assertions.Expect(season3).ToContainTextAsync("saved 9 Sep 2025, 11:55");
        await Assertions.Expect(page.Locator("[data-testid='save-damaged']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task DiscardChanges_ReloadsTheSaveFromTheFolder()
    {
        var page = await Open(("S1", "wd1_saveslot2.bundle", null));
        await SaveItem(page, "wd1_saveslot2.bundle").ClickAsync();
        var choice = page.Locator("[data-testid='season-s1'] details[open] .choice-row").First.Locator("select");
        var original = await choice.InputValueAsync();
        await choice.SelectOptionAsync(original == "0" ? "1" : "0");
        page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

        await page.Locator("[data-testid='discard-btn']").ClickAsync();

        await Assertions.Expect(choice).ToHaveValueAsync(original);
        await Assertions.Expect(page.Locator("[data-testid='discard-btn']")).ToHaveCountAsync(0);
        await Assertions.Expect(SaveItem(page, "wd1_saveslot2.bundle").Locator("[data-testid='save-unsaved']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task MidEpisodeSave_ShowsItsCheckpointAndCanRestartFromTheStart()
    {
        var page = await Open(("S3", "wd3_saveslot1.bundle", null));
        await SaveItem(page, "wd3_saveslot1.bundle").ClickAsync();
        await page.Locator("[data-testid='tab-resume']").ClickAsync();

        var chapter = page.Locator("[data-testid='restart-chapter']");
        await Assertions.Expect(chapter).ToHaveValueAsync("current-position");
        await Assertions.Expect(page.Locator("[data-testid='restart-current']")).ToHaveTextAsync("Current checkpoint: Junkyard Day");
        await Assertions.Expect(page.Locator("[data-testid='restart-current']")).ToBeDisabledAsync();

        var start = await chapter.Locator("option:not([disabled])").First.GetAttributeAsync("value");
        await chapter.SelectOptionAsync(start!);

        await Assertions.Expect(page.Locator("[data-testid='resume-state']")).ToContainTextAsync("from the beginning");
        await Assertions.Expect(page.Locator("[data-testid='restart-current']")).ToHaveCountAsync(0);
        await Assertions.Expect(chapter).ToHaveValueAsync(start!);
    }

    [Fact]
    public async Task PropertyEdits_MarkTheSaveAsChanged()
    {
        var page = await Open(("S2", "wd2_saveslot1.bundle", null));
        await SaveItem(page, "wd2_saveslot1.bundle").ClickAsync();
        await page.Locator("[data-testid='tab-properties']").ClickAsync();
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes");

        var number = page.Locator(".property-editor input[type='number']").First;
        await number.FillAsync("1234");
        await number.DispatchEventAsync("change");

        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Save Changes *");
        await Assertions.Expect(SaveItem(page, "wd2_saveslot1.bundle").Locator("[data-testid='save-unsaved']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NewSave_WarnsBeforeReplacingAnExistingSave()
    {
        var page = await Open(("S1", "wd1_saveslot2.bundle", null));
        await page.Locator("[data-testid='new-save-btn']").ClickAsync();
        var dialog = page.Locator("[data-testid='new-save-dialog']");
        await dialog.Locator("select").First.SelectOptionAsync("s1");

        await Assertions.Expect(dialog.Locator("input[type='text']")).ToHaveValueAsync("wd1_saveslot1.bundle");
        await Assertions.Expect(dialog.Locator("[data-testid='new-save-replaces']")).ToHaveCountAsync(0);
        await Assertions.Expect(dialog.Locator(".btn-accent")).ToHaveTextAsync("Create");

        await dialog.Locator("input[type='text']").FillAsync("WD1_SAVESLOT2");

        await Assertions.Expect(dialog.Locator("[data-testid='new-save-replaces']")).ToContainTextAsync("wd1_saveslot2.bundle already exists");
        await Assertions.Expect(dialog.Locator(".btn-accent")).ToHaveTextAsync("Replace");
    }
}
