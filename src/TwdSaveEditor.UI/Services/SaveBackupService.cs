using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;

namespace TwdSaveEditor.UI.Services;

public class SaveBackupService(IFileSystemService fs, IBackupFileResolver backupFiles)
{
    private const string TimestampFormat = "yyyyMMdd_HHmmss";

    public async Task<string?> BackupBeforeSave(SaveSlot slot)
    {
        var backupFolder = $"backup_{DateTime.Now.ToString(TimestampFormat)}";
        var result = await fs.BackupFiles(backupFolder, [.. backupFiles.GetFilesToBackup(slot)]);
        if (result.Error != null)
        {
            throw new IOException($"Backup failed for {result.Error}. Nothing was changed.");
        }

        return result.Folder;
    }
}
