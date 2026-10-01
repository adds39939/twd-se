using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.Accessors;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Persistence;

namespace TwdSaveEditor.Season.S2.Accessors;

public sealed class S2ChoiceAccessor(SaveSlot slot) : IChoiceAccessor
{
    private readonly SaveAccessor _seasonChoices = new(slot.Choices, slot.Metadata);

    public int DetectCurrentChoice(ChoiceDefinition choice)
    {
        var value = GetChoiceValue(choice.ChoiceKey);
        return value == null
            ? -1
            : Array.FindIndex(choice.Options, option => option.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    public void ApplyChoice(ChoiceDefinition choice, int optionIndex) =>
        SetChoiceValue(choice.ChoiceKey, choice.Options[optionIndex].Value);

    public string? GetChoiceValue(string choiceKey) =>
        IsImportedKey(choiceKey) ? slot.Choices?.GetString(choiceKey) : _seasonChoices.GetChoiceValue(choiceKey);

    public void SetChoiceValue(string choiceKey, string value)
    {
        if (!IsImportedKey(choiceKey))
        {
            _seasonChoices.SetChoiceValue(choiceKey, value);
            return;
        }

        if (slot.Choices == null)
            throw new InvalidOperationException("Cannot set choice value: no choices PropertySet loaded.");

        slot.Choices.SetString(choiceKey, value.ToLowerInvariant());
    }

    private static bool IsImportedKey(string choiceKey) => S1ChoiceCatalog.PersistentEpisode(choiceKey) != null;
}
