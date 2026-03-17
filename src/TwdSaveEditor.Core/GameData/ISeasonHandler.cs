using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.GameData;

public interface ISeasonHandler
{
    string SeasonKey { get; }
    string FilePrefix { get; }
    bool UsesEventLog { get; }
    bool UsesChoiceStats { get; }
    string GetEpisodeId(int episode);
    SaveSlot CreateBlankSave(string fileName, string episodeId);
    IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot);
    void PopulateChoices(SaveSlot slot, int episode);
    bool CanHandle(string fileName);
}
