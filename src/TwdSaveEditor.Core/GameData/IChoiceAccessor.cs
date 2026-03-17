namespace TwdSaveEditor.Core.GameData;

/// <summary>
/// Common interface for accessing choices across different save formats.
/// Implemented by SaveAccessor (S1/S2 choices.prop) and EventLogAccessor (S3/Michonne estore/epage).
/// </summary>
public interface IChoiceAccessor
{
    int DetectCurrentChoice(ChoiceDefinition choice);
    void ApplyChoice(ChoiceDefinition choice, int optionIndex);
    string? GetChoiceValue(string choiceKey);
    void SetChoiceValue(string choiceKey, string value);
}
