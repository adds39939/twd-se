using TwdSaveEditor.UI.Model;

namespace TwdSaveEditor.UI.Services;

public interface IFileSystemService
{
    Task<bool> IsSupported();

    Task<bool> PickDirectory();

    Task<RememberedDirectory?> RememberedDirectory();

    Task<bool> ReopenDirectory();

    Task<string[]> ListFiles();

    Task<byte[]?> ReadFile(string name);

    Task<bool> WriteFile(string name, byte[] data);

    Task<bool> DeleteFile(string name);

    Task<BackupResult> BackupFiles(string folderName, string[] fileNames);

    Task<string> GetDirectoryName();

    Task DownloadFile(string name, byte[] data);
}
