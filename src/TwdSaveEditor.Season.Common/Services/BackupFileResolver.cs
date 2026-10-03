using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;

namespace TwdSaveEditor.Season.Common.Services;

public sealed class BackupFileResolver(ISeasonRegistry registry) : IBackupFileResolver
{
    private const string AutosaveSuffix = "_autosave";
    private const string BundleExtension = ".bundle";

    public IReadOnlyList<string> GetFilesToBackup(SaveSlot slot)
    {
        var fileName = Path.GetFileName(slot.FileName);
        var filesToBackup = new List<string> { fileName };

        var autoName = Path.GetFileNameWithoutExtension(fileName);
        if (autoName.StartsWith('_') && autoName.EndsWith(AutosaveSuffix, StringComparison.Ordinal))
        {
            filesToBackup.Add(autoName[1..^AutosaveSuffix.Length] + BundleExtension);
        }

        if (registry.DetectFromFileName(slot.FileName) is ICompanionFileHandler companion)
        {
            filesToBackup.AddRange(companion.GetCompanionFileNames(slot));
        }

        return filesToBackup;
    }
}
