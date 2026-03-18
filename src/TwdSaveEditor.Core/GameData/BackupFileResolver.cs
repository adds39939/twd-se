using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Determines which files need to be backed up before modifying a save.
/// </summary>
public static class BackupFileResolver
{
    /// <summary>
    /// Get the list of files that should be backed up before modifying the given save slot.
    /// Includes the save itself, plus the corresponding slot bundle for autosaves,
    /// and estore/epage files for S3/Michonne saves.
    /// </summary>
    public static List<string> GetFilesToBackup(SaveSlot slot, ISeasonRegistry? registry = null)
    {
        var fileName = Path.GetFileName(slot.FileName);
        var isAutosave = fileName.StartsWith('_');
        var filesToBackup = new List<string> { fileName };

        if (isAutosave)
        {
            var autoName = Path.GetFileNameWithoutExtension(fileName);
            if (autoName.StartsWith('_') && autoName.EndsWith("_autosave"))
            {
                var slotName = autoName[1..^9] + ".bundle";
                filesToBackup.Add(slotName);
            }
        }

        if (registry is null)
        {
            return filesToBackup;
        }

        var saveHandler = registry.DetectFromFileName(slot.FileName);
        if (saveHandler?.UsesEventLog != true || slot.EStorePath is null)
        {
            return filesToBackup;
        }

        filesToBackup.Add(slot.EStorePath);
        if (slot.EPagePaths != null)
        {
            filesToBackup.AddRange(slot.EPagePaths);
        }

        return filesToBackup;
    }
}
