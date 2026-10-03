using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface ISeasonHandler
{
    string SeasonKey { get; }

    string Name { get; }

    string ShortName { get; }

    string FilePrefix { get; }

    IReadOnlyList<EpisodeInfo> Episodes { get; }

    IReadOnlyList<ChoiceDefinition> Choices { get; }

    IReadOnlyList<string> IncludedSeasonKeys { get; }

    IReadOnlyList<string> ImportsFromSeasonKeys { get; }

    string GetEpisodeId(int episode);

    (string SeasonKey, int Episode) DecisionGroupOf(int episode);

    bool CanHandle(string fileName);

    SaveSlot CreateBlankSave(string fileName, string episodeId);
    IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot);
    void PopulateChoices(SaveSlot slot, int episode);
}
