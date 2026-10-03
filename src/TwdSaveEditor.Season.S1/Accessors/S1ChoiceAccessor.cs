using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Season.S1.Accessors;

public sealed class S1ChoiceAccessor(SaveSlot slot, IS1CheckpointRefresher? checkpoints = null) : IChoiceAccessor
{
    public int DetectCurrentChoice(ChoiceDefinition choice)
    {
        var value = GetChoiceValue(choice.ChoiceKey);
        return value == null
            ? -1
            : Array.FindIndex(choice.Options, option => option.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    public void ApplyChoice(ChoiceDefinition choice, int optionIndex) =>
        SetChoiceValue(choice.ChoiceKey, choice.Options[optionIndex].Value);

    public string? GetChoiceValue(string choiceKey)
    {
        if (slot.Metadata == null || S1ChoiceCatalog.PersistentEpisode(choiceKey) is not { } episode)
        {
            return null;
        }

        var value = slot.Metadata.GetString(PersistentKeys.SlotKey(episode, choiceKey));
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public void SetChoiceValue(string choiceKey, string value)
    {
        if (slot.Metadata == null)
        {
            throw new InvalidOperationException("Cannot set choice value: the save has no slot metadata.");
        }

        if (S1ChoiceCatalog.PersistentEpisode(choiceKey) is not { } episode)
        {
            return;
        }

        value = value.ToLowerInvariant();
        slot.Metadata.SetString(PersistentKeys.SlotKey(episode, choiceKey), value);
        UpdateTracker(episode, choiceKey, value);
        UpdateAutosave(choiceKey, value);
        checkpoints?.Refresh(slot);
    }

    public void ClearChoiceValue(string choiceKey)
    {
        if (slot.Metadata == null || S1ChoiceCatalog.PersistentEpisode(choiceKey) is not { } episode)
        {
            return;
        }

        slot.Metadata.Remove(Symbol.FromString(PersistentKeys.SlotKey(episode, choiceKey)));
        UpdateTracker(episode, choiceKey, null);
        UpdateAutosave(choiceKey, null);
        checkpoints?.Refresh(slot);
    }

    private void UpdateTracker(int episode, string choiceKey, string? value)
    {
        if (slot.Choices == null)
        {
            return;
        }

        var container = Symbol.FromString(PersistentKeys.TrackerContainer(episode));
        var type = new Symbol(TelltaleTypes.ChoicesContainer);
        var entries = slot.Choices.Find(container)?.Value is RawBytesValue existing
            ? ChoicesContainer.Parse(existing.Data)
            : [];

        var prefix = PersistentKeys.TrackerPrefix(choiceKey);
        var removed = entries.RemoveAll(entry => entry.str.StartsWith(prefix, StringComparison.Ordinal));
        if (value == null)
        {
            if (removed == 0)
            {
                return;
            }
        }
        else
        {
            var added = PersistentKeys.TrackerEntry(choiceKey, value);
            var index = entries.FindIndex(entry => string.CompareOrdinal(entry.str, added) > 0);
            entries.Insert(index < 0 ? entries.Count : index, (added, true));
        }

        slot.Choices.Set(container, type, new RawBytesValue(ChoicesContainer.Serialize(entries), type));
    }

    private void UpdateAutosave(string choiceKey, string? value)
    {
        if (slot.Autosave?.FindFile(S1SlotFiles.LogicGameProperties) is not { } logic || !BundleReader.TryParseProperties(logic))
        {
            return;
        }

        if (value == null)
        {
            logic.Properties!.Remove(Symbol.FromString(choiceKey));
        }
        else if (logic.Properties!.Find(choiceKey)?.Value is StringValue current)
        {
            current.Value = value;
        }
    }
}
