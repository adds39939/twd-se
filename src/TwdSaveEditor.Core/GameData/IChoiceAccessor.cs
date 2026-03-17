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

    /// <summary>
    /// Set a choice value. May throw <see cref="InvalidOperationException"/>
    /// if the underlying data store is not available (e.g., no choices PropertySet loaded).
    /// </summary>
    void SetChoiceValue(string choiceKey, string value);
}
