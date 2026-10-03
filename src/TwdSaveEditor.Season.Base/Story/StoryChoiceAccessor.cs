using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StoryChoiceAccessor(SaveSlot slot, StorySeason season) : IChoiceAccessor
{
    private readonly StoryDecisionLog _decisions = new(slot, season);

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
        slot.EventLog != null && season.FindDecision(choiceKey) is { } decision ? _decisions.GetOption(decision)?.Value : null;

    public void SetChoiceValue(string choiceKey, string value)
    {
        if (season.FindDecision(choiceKey) is not { } decision || decision.Find(value) is not { } option)
        {
            return;
        }

        new StoryEventLog(slot, season).Prepare();
        _decisions.SetValue(decision, option);
        UpdateSavedLogic(slot, season);
    }

    public void ClearChoiceValue(string choiceKey)
    {
        if (slot.EventLog == null || season.FindDecision(choiceKey) is not { } decision)
        {
            return;
        }

        _decisions.Clear(decision);
        UpdateSavedLogic(slot, season);
    }

    public static void UpdateSavedLogic(SaveSlot slot, StorySeason season)
    {
        var nodes = new StoryEventLog(slot, season).Nodes();
        foreach (var save in slot.Checkpoints)
        {
            if (StoryResumePoint.Properties(save, StoryFiles.LogicGameProperties) is not { } game)
            {
                continue;
            }

            var episode = save.Metadata?.GetInt(SaveMetadataKeys.Episode) ?? season.LastEpisode;
            foreach (var key in season.LogicKeys.Where(key => key.ReadFrom <= episode))
            {
                var current = game.Find(key.Key)?.Value;
                var wanted = StoryDecisionLog.Evaluate(key, nodes);
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
