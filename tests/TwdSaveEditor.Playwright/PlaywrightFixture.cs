using System.Diagnostics;
using Microsoft.Playwright;

namespace TwdSaveEditor.Playwright;

public class PlaywrightFixture : IAsyncLifetime
{
    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl { get; private set; } = null!;

    private Process? _serverProcess;

    public async Task InitializeAsync()
    {
        // Install Playwright browsers if needed
        Program.Main(["install", "chromium"]);

        // Start the Blazor dev server
        var webProjectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "TwdSaveEditor.Web"));

        // Use a fixed port for simplicity
        const int port = 5280;
        BaseUrl = $"http://localhost:{port}";

        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{webProjectPath}\" --urls {BaseUrl}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        _serverProcess.Start();

        // Wait for server to be ready
        using var client = new HttpClient();
        for (var i = 0; i < 60; i++)
        {
            try
            {
                var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode) break;
            }
            catch
            {
                // Server not ready yet
            }

            await Task.Delay(1000);
        }

        // Create browser
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();

        if (_serverProcess is { HasExited: false })
        {
            _serverProcess.Kill(true);
            _serverProcess.Dispose();
        }
    }

    public async Task<IPage> NewPage()
    {
        var page = await Browser.NewPageAsync();
        await page.GotoAsync(BaseUrl);

        // Wait for Blazor to initialize
        await page.WaitForSelectorAsync("[data-testid='app-ready']",
            new PageWaitForSelectorOptions { Timeout = 30000 });

        return page;
    }
}
