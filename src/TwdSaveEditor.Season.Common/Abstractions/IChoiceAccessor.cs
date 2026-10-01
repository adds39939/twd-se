using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface IChoiceAccessor
{
    int DetectCurrentChoice(ChoiceDefinition choice);
    void ApplyChoice(ChoiceDefinition choice, int optionIndex);
    string? GetChoiceValue(string choiceKey);

    void SetChoiceValue(string choiceKey, string value);
}
