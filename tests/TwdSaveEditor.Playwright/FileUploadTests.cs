using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright;

[Collection(PlaywrightCollection.Name)]
public class FileUploadTests
{
    private readonly PlaywrightFixture _fixture;

    public FileUploadTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task App_ShowsEmptyStateWhenNoDirectory()
    {
        var page = await _fixture.NewPage();

        var emptyState = page.Locator(".empty-state");
        await Assertions.Expect(emptyState).ToBeVisibleAsync();

        var emptyText = await emptyState.TextContentAsync();
        Assert.NotNull(emptyText);
        Assert.Contains("save directory", emptyText);
    }

    [Fact]
    public async Task SaveList_IsEmptyInitially()
    {
        var page = await _fixture.NewPage();

        var saveList = page.Locator("[data-testid='save-list']");
        await Assertions.Expect(saveList).ToBeVisibleAsync();

        var saveItems = saveList.Locator(".save-item");
        await Assertions.Expect(saveItems).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task OpenDirectoryButton_IsNotDisabledInitially()
    {
        var page = await _fixture.NewPage();

        var button = page.Locator("[data-testid='open-directory']");
        await Assertions.Expect(button).ToBeEnabledAsync();
    }
}
