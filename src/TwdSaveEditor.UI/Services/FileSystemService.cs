using Microsoft.JSInterop;

namespace TwdSaveEditor.UI.Services;

public class FileSystemService : IFileSystemService, IAsyncDisposable
{
    private const string ModulePath = "./_content/TwdSaveEditor.UI/js/fileSystem.js";

    private readonly Lazy<Task<IJSObjectReference>> _module;

    public FileSystemService(IJSRuntime js)
    {
        _module = new(() => js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());
    }

    public async Task<bool> IsSupported() =>
        await (await _module.Value).InvokeAsync<bool>("isSupported");

    public async Task<bool> PickDirectory() =>
        await (await _module.Value).InvokeAsync<bool>("pickDirectory");

    public async Task<string[]> ListFiles(string extension) =>
        await (await _module.Value).InvokeAsync<string[]>("listFiles", extension);

    public async Task<byte[]?> ReadFile(string name)
    {
        var base64 = await (await _module.Value).InvokeAsync<string?>("readFile", name);
        if (base64 == null) return null;
        return Convert.FromBase64String(base64);
    }

    public async Task<bool> WriteFile(string name, byte[] data)
    {
        var base64 = Convert.ToBase64String(data);
        return await (await _module.Value).InvokeAsync<bool>("writeFile", name, base64);
    }

    public async Task<bool> BackupFiles(string folderName, string[] fileNames) =>
        await (await _module.Value).InvokeAsync<bool>("backupFiles", folderName, fileNames);

    public async Task<bool> HasDirectory() =>
        await (await _module.Value).InvokeAsync<bool>("hasDirectory");

    public async Task<string> GetDirectoryName() =>
        await (await _module.Value).InvokeAsync<string>("getDirectoryName");

    public async Task DownloadFile(string name, byte[] data) =>
        await (await _module.Value).InvokeVoidAsync("downloadFile", name, Convert.ToBase64String(data));

    public async ValueTask DisposeAsync()
    {
        if (!_module.IsValueCreated) return;

        try
        {
            await (await _module.Value).DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
