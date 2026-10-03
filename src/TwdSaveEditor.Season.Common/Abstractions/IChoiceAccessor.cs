using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Common.Abstractions;

public interface IChoiceAccessor
{
    int DetectCurrentChoice(ChoiceDefinition choice);

    IReadOnlyList<int> DetectCurrentChoices(IReadOnlyList<ChoiceDefinition> choices) => [.. choices.Select(DetectCurrentChoice)];

    void ApplyChoice(ChoiceDefinition choice, int optionIndex);

    string? GetChoiceValue(string choiceKey);

    void SetChoiceValue(string choiceKey, string value);

    void ClearChoiceValue(string choiceKey);
}
