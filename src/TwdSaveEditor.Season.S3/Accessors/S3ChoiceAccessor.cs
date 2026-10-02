using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S3.Decisions;
using TwdSaveEditor.Season.S3.Saves;

namespace TwdSaveEditor.Season.S3.Accessors;

public sealed class S3ChoiceAccessor(SaveSlot slot) : IChoiceAccessor
{
    private readonly S3DecisionLog _decisions = new(slot);

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
        slot.EventLog != null && S3DecisionCatalog.Find(choiceKey) is { } decision ? _decisions.GetOption(decision)?.Value : null;

    public void SetChoiceValue(string choiceKey, string value)
    {
        if (S3DecisionCatalog.Find(choiceKey) is not { } decision || decision.Find(value) is not { } option)
            return;

        new S3EventLog(slot).EnsurePreviousGameData();
        _decisions.SetValue(decision, option);
        UpdateSavedLogic(slot);
    }

    public static void UpdateSavedLogic(SaveSlot slot)
    {
        var nodes = new S3EventLog(slot).Nodes();
        foreach (var save in slot.Checkpoints)
        {
            if (S3ResumePoint.Properties(save, S3SlotFiles.LogicGameProperties) is not { } game)
                continue;

            foreach (var key in S3DecisionCatalog.LogicKeys)
            {
                var current = game.Find(key.Key)?.Value;
                var wanted = S3DecisionLog.Evaluate(key, nodes);
                if (current is BoolValue flag && wanted is bool expected && flag.Value != expected)
                {
                    flag.Value = expected;
                    save.Modified = true;
                }
                else if (current is StringValue text && wanted is string name && text.Value != name)
                {
                    text.Value = name;
                    save.Modified = true;
                }
            }
        }
    }
}
