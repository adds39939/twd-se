using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;

namespace TwdSaveEditor.Season.Common.Services;

public static class BackupFileResolver
{
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

        if (registry?.DetectFromFileName(slot.FileName) is ICompanionFileHandler companion)
        {
            filesToBackup.AddRange(companion.GetCompanionFileNames(slot));
        }

        return filesToBackup;
    }
}
