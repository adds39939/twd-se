using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface IBackupFileResolver
{
    IReadOnlyList<string> GetFilesToBackup(SaveSlot slot);
}
