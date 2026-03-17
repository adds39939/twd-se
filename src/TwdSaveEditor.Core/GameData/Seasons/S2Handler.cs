using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.GameData.Seasons;

public class S2Handler : ISeasonHandler
{
    public string SeasonKey => "s2";
    public string FilePrefix => "wd2_";
    public bool UsesEventLog => false;
    public bool UsesChoiceStats => false;

    public string GetEpisodeId(int episode) => $"WalkingDead20{episode}";

    public SaveSlot CreateBlankSave(string fileName, string episodeId)
        => SaveSlotFactory.CreateBlankS1S2(fileName, episodeId);

    public IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot)
        => slot.Choices != null ? new SaveAccessor(slot.Choices, slot.Metadata) : null;

    public void PopulateChoices(SaveSlot slot, int episode)
    {
        var choices = ChoiceDatabase.ForSeason(SeasonKey)
            .Where(c => c.Episode <= episode)
            .ToList();

        if (choices.Count == 0)
            return;

        var entries = choices
            .Select(c => ($"{c.ChoiceKey} - {c.Options[0].Value}", true))
            .ToList();

        var typeSymbol = new Symbol(TelltaleTypes.ChoicesContainer);
        var group = slot.Choices!.TypeGroups.FirstOrDefault(g =>
            g.TypeSymbol.Value == TelltaleTypes.ChoicesContainer);

        if (group == null)
        {
            group = new TypeGroup(typeSymbol);
            slot.Choices!.TypeGroups.Add(group);
        }

        var raw = SaveAccessor.SerializeStringBoolArray(entries);
        if (group.Properties.Count > 0)
        {
            group.Properties[0] = new Property(
                group.Properties[0].KeySymbol,
                new RawBytesValue(raw, typeSymbol));
        }
        else
        {
            group.Properties.Add(new Property(
                Symbol.FromString($"{SeasonKey}_choices"),
                new RawBytesValue(raw, typeSymbol)));
        }
    }

    public bool CanHandle(string fileName)
    {
        var name = Path.GetFileName(fileName).ToLowerInvariant();
        if (name.StartsWith('_')) name = name[1..];
        return name.StartsWith("wd2_");
    }
}
