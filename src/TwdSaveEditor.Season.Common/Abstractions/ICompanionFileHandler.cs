using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface ICompanionFileHandler
{
    bool IsCompanionFile(string fileName);

    IReadOnlyList<string> FindCompanionFiles(string bundleFileName, IEnumerable<string> directoryFileNames);

    void AttachCompanionFiles(SaveSlot slot, IReadOnlyList<CompanionFile> files);

    IReadOnlyList<CompanionFile> BuildCompanionFiles(SaveSlot slot);

    IReadOnlyList<string> GetCompanionFileNames(SaveSlot slot);
}
