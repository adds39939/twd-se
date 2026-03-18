using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Web.Services;

/// <summary>
/// Backs up save files before they are modified.
/// Creates a timestamped subfolder in the save directory containing
/// copies of all files that will be affected by the save operation.
/// </summary>
public class SaveBackupService
{
    private readonly FileSystemService _fs;
    private readonly ISeasonRegistry _registry;

    public SaveBackupService(FileSystemService fs, ISeasonRegistry registry)
    {
        _fs = fs;
        _registry = registry;
    }

    /// <summary>
    /// Back up all files that will be modified when saving the given slot.
    /// Returns the backup folder name, or null if backup failed.
    /// </summary>
    public async Task<string?> BackupBeforeSave(SaveSlot slot)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFolder = $"backup_{timestamp}";
        var filesToBackup = BackupFileResolver.GetFilesToBackup(slot, _registry);

        var success = await _fs.BackupFiles(backupFolder, filesToBackup.ToArray());
        return success ? backupFolder : null;
    }
}
