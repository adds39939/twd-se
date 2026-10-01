using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface IChoiceImporter
{
    bool CanImportFrom(SaveSlot source);

    void ImportChoices(SaveSlot source, SaveSlot target);
}
