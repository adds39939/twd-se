namespace TwdSaveEditor.UI.Services;

public interface IFileSystemService
{
    Task<bool> IsSupported();

    Task<bool> PickDirectory();

    Task<string[]> ListFiles(string extension);

    Task<byte[]?> ReadFile(string name);

    Task<bool> WriteFile(string name, byte[] data);

    Task<bool> DeleteFile(string name);

    Task<bool> BackupFiles(string folderName, string[] fileNames);

    Task<bool> HasDirectory();

    Task<string> GetDirectoryName();

    Task DownloadFile(string name, byte[] data);
}
