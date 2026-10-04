using Microsoft.Playwright;
using TwdSaveEditor.Playwright.Fixtures;

namespace TwdSaveEditor.Playwright.Tests;

[Collection(PlaywrightCollection.Name)]
public class ServiceWorkerTests
{
    private const string OtherSiteScript = "https://analytics.example.test/beacon.min.js";

    private readonly PlaywrightFixture _fixture;

    public ServiceWorkerTests(PlaywrightFixture fixture) => _fixture = fixture;

    private async Task<IPage> OpenControlledPage()
    {
        var page = await _fixture.NewPage();
        await page.EvaluateAsync("() => navigator.serviceWorker.ready.then(() => true)");
        await page.ReloadAsync();
        await page.WaitForFunctionAsync("() => navigator.serviceWorker.controller !== null");
        await page.WaitForSelectorAsync("[data-testid='app-ready']", new PageWaitForSelectorOptions { Timeout = 30000 });
        return page;
    }

    [Fact]
    public async Task ServiceWorker_AnswersThePageAndAppFilesFromItsCache()
    {
        var page = await OpenControlledPage();

        var appScript = page.WaitForResponseAsync(response => response.Url.Contains("/_framework/blazor.webassembly"));
        var document = await page.ReloadAsync();

        Assert.NotNull(document);
        Assert.True(document.FromServiceWorker);
        Assert.True((await appScript).FromServiceWorker);
    }

    [Fact]
    public async Task ServiceWorker_LeavesOtherSitesRequestsToTheBrowser()
    {
        var page = await OpenControlledPage();
        await page.Context.RouteAsync(OtherSiteScript, route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "text/javascript",
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
            Body = string.Empty,
        }));

        var response = await page.RunAndWaitForResponseAsync(
            () => page.EvaluateAsync($"() => fetch('{OtherSiteScript}').then(response => response.status)"),
            OtherSiteScript);

        Assert.Equal(200, response.Status);
        Assert.False(response.FromServiceWorker);
    }
}
