using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Services;

namespace TwdSaveEditor.UI.Services;

public class SaveBackupService
{
    private readonly IFileSystemService _fs;
    private readonly ISeasonRegistry _registry;

    public SaveBackupService(IFileSystemService fs, ISeasonRegistry registry)
    {
        _fs = fs;
        _registry = registry;
    }

    public async Task<string?> BackupBeforeSave(SaveSlot slot)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFolder = $"backup_{timestamp}";
        var filesToBackup = BackupFileResolver.GetFilesToBackup(slot, _registry);

        var success = await _fs.BackupFiles(backupFolder, filesToBackup.ToArray());
        return success ? backupFolder : null;
    }
}
