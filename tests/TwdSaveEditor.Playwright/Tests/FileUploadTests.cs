using System.IO.Compression;
using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;
using TwdSaveEditor.Playwright.Support;

namespace TwdSaveEditor.Playwright.Tests;

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

    private async Task<IPage> UploadOnlyPage()
    {
        var page = await _fixture.Browser.NewPageAsync();
        await page.AddInitScriptAsync("window.showDirectoryPicker = undefined;");
        await page.GotoAsync(_fixture.BaseUrl);
        await page.WaitForSelectorAsync("[data-testid='app-ready']", new() { Timeout = 30000 });
        return page;
    }

    [Fact]
    public async Task UploadedSave_DownloadsItsChangedFilesAsAZip()
    {
        var page = await UploadOnlyPage();
        await Assertions.Expect(page.Locator("[data-testid='new-save-btn']")).ToBeVisibleAsync();
        var upload = page.Locator("input[type='file']");
        await upload.SetInputFilesAsync([TestDataHelper.GetPath("S1", "wd1_saveslot2.bundle"), TestDataHelper.GetPath("S1", "_wd1_saveslot2_autosave.bundle")]);
        await page.Locator(".save-item").First.ClickAsync();

        var row = page.Locator("details[open] details[open] .choice-row").First;
        var description = await row.Locator(".choice-desc").TextContentAsync();
        var choice = row.Locator("select");
        var changed = await choice.InputValueAsync() == "0" ? "1" : "0";
        await choice.SelectOptionAsync(changed);
        await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Download Changes *");

        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator(".save-btn").ClickAsync());
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            ZipFile.ExtractToDirectory(await download.PathAsync(), folder);
            Assert.Equal("wd1_saveslot2.zip", download.SuggestedFilename);
            Assert.Equal(["_wd1_saveslot2_autosave.bundle", "wd1_saveslot2.bundle"], Directory.GetFiles(folder).Select(Path.GetFileName).Order());
            await Assertions.Expect(page.Locator(".save-btn")).ToHaveTextAsync("Download Changes");

            await upload.SetInputFilesAsync(Directory.GetFiles(folder));
            await page.Locator(".save-item").First.ClickAsync();
            await Assertions.Expect(page.Locator(".choice-row", new() { HasTextString = description! }).First.Locator("select")).ToHaveValueAsync(changed);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task OpenDirectoryButton_IsNotDisabledInitially()
    {
        var page = await _fixture.NewPage();

        var button = page.Locator("[data-testid='open-directory']");
        await Assertions.Expect(button).ToBeEnabledAsync();
    }
}
