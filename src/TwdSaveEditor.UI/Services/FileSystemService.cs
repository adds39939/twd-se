using Microsoft.JSInterop;
using TwdSaveEditor.UI.Model;

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

    public async Task<RememberedDirectory?> RememberedDirectory() =>
        await (await _module.Value).InvokeAsync<RememberedDirectory?>("rememberedDirectory");

    public async Task<bool> ReopenDirectory() =>
        await (await _module.Value).InvokeAsync<bool>("reopenDirectory");

    public async Task<string[]> ListFiles() =>
        await (await _module.Value).InvokeAsync<string[]>("listFiles");

    public async Task<byte[]?> ReadFile(string name) =>
        await (await _module.Value).InvokeAsync<byte[]?>("readFile", name);

    public async Task<bool> WriteFile(string name, byte[] data) =>
        await (await _module.Value).InvokeAsync<bool>("writeFile", name, data);

    public async Task<bool> DeleteFile(string name) =>
        await (await _module.Value).InvokeAsync<bool>("deleteFile", name);

    public async Task<BackupResult> BackupFiles(string folderName, string[] fileNames) =>
        await (await _module.Value).InvokeAsync<BackupResult>("backupFiles", folderName, fileNames);

    public async Task<string> GetDirectoryName() =>
        await (await _module.Value).InvokeAsync<string>("getDirectoryName");

    public async Task DownloadFile(string name, byte[] data) =>
        await (await _module.Value).InvokeVoidAsync("downloadFile", name, data);

    public async ValueTask DisposeAsync()
    {
        if (!_module.IsValueCreated)
        {
            return;
        }

        try
        {
            await (await _module.Value).DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
