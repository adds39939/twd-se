using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class NewSaveTests
{
    private readonly PlaywrightFixture _fixture;

    public NewSaveTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

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

    private static async Task<ILocator> OpenDialog(IPage page, string seasonKey)
    {
        await page.Locator("[data-testid='new-save-btn']").ClickAsync();
        var dialog = page.Locator("[data-testid='new-save-dialog']");
        await dialog.Locator("select").First.SelectOptionAsync(seasonKey);
        return dialog;
    }

    [Fact]
    public async Task NewSaveDialog_ExistsInDom()
    {
        var page = await _fixture.NewPage();

        var dialog = page.Locator("[data-testid='new-save-dialog']");
        await Assertions.Expect(dialog).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task NewSaveDialog_HasSeasonSelector()
    {
        var page = await _fixture.NewPage();

        var seasonSelects = page.Locator("[data-testid='new-save-dialog'] select").First;
        var options = seasonSelects.Locator("option");

        var count = await options.CountAsync();
        Assert.True(count >= 6, $"Expected at least 6 season options, got {count}");
    }

    [Fact]
    public async Task NewSaveDialog_HasEpisodeSelector()
    {
        var page = await _fixture.NewPage();

        var episodeSelect = page.Locator("[data-testid='new-save-dialog'] select").Nth(1);
        var options = episodeSelect.Locator("option");

        var count = await options.CountAsync();
        Assert.True(count >= 1, $"Expected at least 1 episode option, got {count}");
    }

    [Fact]
    public async Task NewSaveDialog_ShowsTheFileNameWithoutLettingItBeChanged()
    {
        var page = await Open(("S1", "wd1_saveslot2.bundle", null));
        var dialog = await OpenDialog(page, "s1");

        await Assertions.Expect(dialog.Locator("[data-testid='new-save-file-name']")).ToHaveTextAsync("wd1_saveslot1.bundle");
        await Assertions.Expect(dialog.Locator("input")).ToHaveCountAsync(0);
        await Assertions.Expect(dialog.Locator("[data-testid='new-save-slot-warning']")).ToHaveCountAsync(0);

        await dialog.Locator("select").First.SelectOptionAsync("michonne");
        await Assertions.Expect(dialog.Locator("[data-testid='new-save-file-name']")).ToHaveTextAsync("wdm_saveslot1.bundle");
    }

    [Fact]
    public async Task NewSave_SkipsSlotsThatAlreadyHaveFiles()
    {
        var page = await Open(("S1", "wd1_saveslot2.bundle", null), ("S1", "_wd1_saveslot1_autosave.bundle", null));
        var dialog = await OpenDialog(page, "s1");

        await Assertions.Expect(dialog.Locator("[data-testid='new-save-file-name']")).ToHaveTextAsync("wd1_saveslot3.bundle");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(2, new() { Timeout = 10000 });
        await Assertions.Expect(page.Locator(".toast-success", new() { HasTextString = "Created wd1_saveslot3.bundle" })).ToBeVisibleAsync();
        var files = await FakeSaveDirectory.GetFileNamesAsync(page);
        Assert.Contains("wd1_saveslot3.bundle", files);
        Assert.Contains("_wd1_saveslot1_autosave.bundle", files);
        Assert.Contains("_wd1_saveslot2_autosave.bundle", files);
    }

    [Fact]
    public async Task NewSave_WarnsWhenEveryGameSlotIsInUse()
    {
        var page = await Open(
            ("S1", "wd1_saveslot2.bundle", "wd1_saveslot1.bundle"),
            ("S1", "wd1_saveslot2.bundle", null),
            ("S1", "wd1_saveslot2.bundle", "wd1_saveslot3.bundle"));
        var dialog = await OpenDialog(page, "s1");
        var warning = dialog.Locator("[data-testid='new-save-slot-warning']");

        await Assertions.Expect(dialog.Locator("[data-testid='new-save-file-name']")).ToHaveTextAsync("wd1_saveslot4.bundle");
        await Assertions.Expect(warning).ToContainTextAsync("Season 1 only has 3 save slots");
        await Assertions.Expect(warning).ToContainTextAsync("won't load this new save unless it replaces an existing one");

        await dialog.Locator("select").First.SelectOptionAsync("s1_400days");
        await Assertions.Expect(dialog.Locator("[data-testid='new-save-file-name']")).ToHaveTextAsync("wd1_saveslot4.bundle");
        await Assertions.Expect(warning).ToBeVisibleAsync();

        await dialog.Locator("select").First.SelectOptionAsync("s3");
        await Assertions.Expect(dialog.Locator("[data-testid='new-save-file-name']")).ToHaveTextAsync("wd3_saveslot1.bundle");
        await Assertions.Expect(warning).ToHaveCountAsync(0);

        await dialog.Locator("select").First.SelectOptionAsync("s1");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        await Assertions.Expect(page.Locator(".save-item")).ToHaveCountAsync(4, new() { Timeout = 10000 });
        Assert.Contains("wd1_saveslot4.bundle", await FakeSaveDirectory.GetFileNamesAsync(page));
    }

    [Fact]
    public async Task NewSave_WarnsOnlyAfterFourSlotsForLaterSeasons()
    {
        var page = await Open(
            ("S3", "wd3_saveslot1.bundle", null),
            ("S3", "wd3_saveslot1.bundle", "wd3_saveslot2.bundle"),
            ("S3", "wd3_saveslot1.bundle", "wd3_saveslot3.bundle"));
        var dialog = await OpenDialog(page, "s3");

        await Assertions.Expect(dialog.Locator("[data-testid='new-save-file-name']")).ToHaveTextAsync("wd3_saveslot4.bundle");
        await Assertions.Expect(dialog.Locator("[data-testid='new-save-slot-warning']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task NewSaveButton_NotVisibleWithoutDirectory()
    {
        var page = await _fixture.NewPage();

        var newSaveBtn = page.Locator("[data-testid='new-save-btn']");
        await Assertions.Expect(newSaveBtn).ToHaveCountAsync(0);
    }
}
