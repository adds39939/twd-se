using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace TwdSaveEditor.Playwright.Support;

public sealed class PublishedSite : IAsyncDisposable
{
    private const string Solution = "TwdSaveEditor.slnx";
    private const string WebProject = "src/TwdSaveEditor.Web/TwdSaveEditor.Web.csproj";
    private const string Configuration = "Release";
    private const string UnresolvedPlaceholder = "#[.{fingerprint}]";

    private readonly WebApplication _server;

    private PublishedSite(WebApplication server, string url)
    {
        _server = server;
        Url = url;
    }

    public string Url { get; }

    public static async Task<PublishedSite> StartAsync()
    {
        var output = Path.Combine(AppContext.BaseDirectory, "site");
        await PublishAsync(output);

        var site = Path.Combine(output, "wwwroot");
        if (File.ReadAllText(Path.Combine(site, "index.html")).Contains(UnresolvedPlaceholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The published index.html still has {UnresolvedPlaceholder} placeholders, which an incremental build can leave after index.html changes. Clean TwdSaveEditor.Web and run the tests again.");
        }

        var files = new PhysicalFileProvider(site);
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var server = builder.Build();
        server.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        server.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true });
        await server.StartAsync();

        return new PublishedSite(server, server.Urls.First());
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync();
        await _server.DisposeAsync();
    }

    private static async Task PublishAsync(string output)
    {
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }

        using var publish = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "publish", Path.Combine(RepositoryRoot(), WebProject), "-c", Configuration, "-o", output, "-nologo" },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        })!;

        var standardOutput = publish.StandardOutput.ReadToEndAsync();
        var standardError = publish.StandardError.ReadToEndAsync();
        await publish.WaitForExitAsync();

        if (publish.ExitCode != 0)
        {
            throw new InvalidOperationException($"Publishing the site failed:{Environment.NewLine}{await standardOutput}{await standardError}");
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, Solution)))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"Could not find {Solution} above {AppContext.BaseDirectory}.");
    }
}
