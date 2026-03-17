using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright;

[Collection(PlaywrightCollection.Name)]
public class NewSaveTests
{
    private readonly PlaywrightFixture _fixture;

    public NewSaveTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NewSaveDialog_ExistsInDom()
    {
        var page = await _fixture.NewPage();

        // The dialog element should exist in the DOM even when closed
        var dialog = page.Locator("[data-testid='new-save-dialog']");
        await Assertions.Expect(dialog).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task NewSaveDialog_HasSeasonSelector()
    {
        var page = await _fixture.NewPage();

        // The dialog has a season select with options for all seasons
        var seasonSelects = page.Locator("[data-testid='new-save-dialog'] select").First;
        var options = seasonSelects.Locator("option");

        // Should have 6 seasons: S1, 400 Days, S2, Michonne, S3, S4
        var count = await options.CountAsync();
        Assert.True(count >= 6, $"Expected at least 6 season options, got {count}");
    }

    [Fact]
    public async Task NewSaveDialog_HasEpisodeSelector()
    {
        var page = await _fixture.NewPage();

        // The second select in the dialog is the episode selector
        var episodeSelect = page.Locator("[data-testid='new-save-dialog'] select").Nth(1);
        var options = episodeSelect.Locator("option");

        // Season 1 is selected by default, which has 5 episodes
        var count = await options.CountAsync();
        Assert.True(count >= 1, $"Expected at least 1 episode option, got {count}");
    }

    [Fact]
    public async Task NewSaveDialog_HasFileNameInput()
    {
        var page = await _fixture.NewPage();

        var input = page.Locator("[data-testid='new-save-dialog'] input[type='text']");
        await Assertions.Expect(input).ToHaveCountAsync(1);

        // Default file name should be pre-filled
        var value = await input.InputValueAsync();
        Assert.NotNull(value);
        Assert.Contains(".bundle", value);
    }

    [Fact]
    public async Task NewSaveButton_NotVisibleWithoutDirectory()
    {
        var page = await _fixture.NewPage();

        // The "New Save" button only appears after a directory is loaded
        var newSaveBtn = page.Locator("[data-testid='new-save-btn']");
        await Assertions.Expect(newSaveBtn).ToHaveCountAsync(0);
    }
}
