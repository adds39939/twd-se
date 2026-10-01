using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Common.Abstractions;

namespace TwdSaveEditor.Season.Base.Handlers;

public abstract class PropChoicesSeasonHandler : SeasonHandlerBase
{
    protected virtual string ChoicesFileName => "choices.prop";

    public override SaveSlot CreateBlankSave(string fileName, string episodeId)
        => SaveSlotFactory.CreateBlankWithChoices(fileName, episodeId, ChoicesFileName);

    public override IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot)
        => slot.Choices != null ? new SaveAccessor(slot.Choices, slot.Metadata) : null;

    public override void PopulateChoices(SaveSlot slot, int episode)
    {
        var choices = GetChoicesUpTo(episode);

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

        var raw = ChoicesContainer.Serialize(entries);
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
}
