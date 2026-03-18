using Microsoft.JSInterop;

namespace TwdSaveEditor.Web.Services;

public class FileSystemService
{
    private readonly IJSRuntime _js;

    public FileSystemService(IJSRuntime js) => _js = js;

    public async Task<bool> IsSupported() =>
        await _js.InvokeAsync<bool>("fileSystemApi.isSupported");

    public async Task<bool> PickDirectory() =>
        await _js.InvokeAsync<bool>("fileSystemApi.pickDirectory");

    public async Task<string[]> ListFiles(string extension) =>
        await _js.InvokeAsync<string[]>("fileSystemApi.listFiles", extension);

    public async Task<byte[]?> ReadFile(string name)
    {
        var base64 = await _js.InvokeAsync<string?>("fileSystemApi.readFile", name);
        if (base64 == null) return null;
        return Convert.FromBase64String(base64);
    }

    public async Task<bool> WriteFile(string name, byte[] data)
    {
        var base64 = Convert.ToBase64String(data);
        return await _js.InvokeAsync<bool>("fileSystemApi.writeFile", name, base64);
    }

    public async Task<bool> BackupFiles(string folderName, string[] fileNames) =>
        await _js.InvokeAsync<bool>("fileSystemApi.backupFiles", folderName, fileNames);

    public async Task<bool> HasDirectory() =>
        await _js.InvokeAsync<bool>("fileSystemApi.hasDirectory");

    public async Task<string> GetDirectoryName() =>
        await _js.InvokeAsync<string>("fileSystemApi.getDirectoryName");

    public async Task DownloadFile(string name, byte[] data) =>
        await _js.InvokeVoidAsync("fileSystemApi.downloadFile", name, Convert.ToBase64String(data));
}
