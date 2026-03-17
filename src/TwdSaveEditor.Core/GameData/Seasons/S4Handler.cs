using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.GameData.Seasons;

public class S4Handler : ISeasonHandler
{
    public string SeasonKey => "s4";
    public string FilePrefix => "wd4_";
    public bool UsesEventLog => false;
    public bool UsesChoiceStats => true;

    public string GetEpisodeId(int episode) => $"WalkingDead40{episode}";

    public SaveSlot CreateBlankSave(string fileName, string episodeId)
        => SaveSlotFactory.CreateBlankS4(fileName, episodeId);

    public IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot)
        => slot.ChoiceStats != null ? new ChoiceStatsAccessor(slot) : null;

    public void PopulateChoices(SaveSlot slot, int episode)
    {
        var choices = ChoiceDatabase.ForSeason(SeasonKey)
            .Where(c => c.Episode <= episode)
            .ToList();

        if (choices.Count == 0)
            return;

        var accessor = new ChoiceStatsAccessor(slot);
        foreach (var c in choices)
            accessor.ApplyChoice(c, 0);
    }

    public bool CanHandle(string fileName)
    {
        var name = Path.GetFileName(fileName).ToLowerInvariant();
        if (name.StartsWith('_')) name = name[1..];
        return name.StartsWith("wd4_");
    }
}
