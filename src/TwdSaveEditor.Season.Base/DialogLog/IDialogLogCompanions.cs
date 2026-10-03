using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.DialogLog;

public interface IDialogLogCompanions
{
    IReadOnlyList<string> Find(string bundleFileName, IEnumerable<string> directoryFileNames);

    void Attach(SaveSlot slot, IReadOnlyList<CompanionFile> files, string seasonKey);

    IReadOnlyList<CompanionFile> Build(SaveSlot slot);

    IReadOnlyList<string> Names(SaveSlot slot);
}
