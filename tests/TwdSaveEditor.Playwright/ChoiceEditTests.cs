using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright;

/// <summary>
/// Tests for the DecisionEditor component. Since we cannot use the File System
/// Access API in headless mode to load actual save files, these tests verify
/// the initial UI state and structure.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class ChoiceEditTests
{
    private readonly PlaywrightFixture _fixture;

    public ChoiceEditTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DecisionEditor_NotVisibleWithoutSave()
    {
        var page = await _fixture.NewPage();

        // Without a save loaded, the decision editor should not appear
        var decisionEditor = page.Locator(".decision-editor");
        await Assertions.Expect(decisionEditor).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TabBar_NotVisibleWithoutSave()
    {
        var page = await _fixture.NewPage();

        // Tab buttons should not exist when no save is selected
        var decisionsTab = page.Locator("[data-testid='tab-decisions']");
        await Assertions.Expect(decisionsTab).ToHaveCountAsync(0);

        var propertiesTab = page.Locator("[data-testid='tab-properties']");
        await Assertions.Expect(propertiesTab).ToHaveCountAsync(0);

        var resumeTab = page.Locator("[data-testid='tab-resume']");
        await Assertions.Expect(resumeTab).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task EmptyState_ShowsInstructionText()
    {
        var page = await _fixture.NewPage();

        var heading = page.Locator(".empty-state h2");
        await Assertions.Expect(heading).ToHaveTextAsync("The Walking Dead Save Editor");

        var description = page.Locator(".empty-state p").First;
        var text = await description.TextContentAsync();
        Assert.NotNull(text);
        Assert.Contains("save directory", text);
    }
}
