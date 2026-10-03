using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;

namespace TwdSaveEditor.Season.Base.Story;

public sealed class StoryChoiceAccessor(SaveSlot slot, StorySeason season) : IChoiceAccessor
{
    private readonly StoryDecisionLog _decisions = new(slot, season);

    public int DetectCurrentChoice(ChoiceDefinition choice) => IndexOf(choice, GetChoiceValue(choice.ChoiceKey));

    public IReadOnlyList<int> DetectCurrentChoices(IReadOnlyList<ChoiceDefinition> choices)
    {
        var nodes = new StoryEventLog(slot, season).Nodes();
        return [.. choices.Select(choice => IndexOf(choice, ValueOf(choice.ChoiceKey, nodes)))];
    }

    public void ApplyChoice(ChoiceDefinition choice, int optionIndex) =>
        SetChoiceValue(choice.ChoiceKey, choice.Options[optionIndex].Value);

    public string? GetChoiceValue(string choiceKey) => ValueOf(choiceKey, new StoryEventLog(slot, season).Nodes());

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
            if (DialogLogSaves.FindRuntimeProperties(save, StoryFiles.LogicGameProperties) is not { } game)
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

    private string? ValueOf(string choiceKey, IReadOnlySet<ulong> nodes) =>
        slot.EventLog != null && season.FindDecision(choiceKey) is { } decision ? StoryDecisionLog.Current(decision, nodes)?.Value : null;

    private static int IndexOf(ChoiceDefinition choice, string? value) =>
        value == null ? -1 : Array.FindIndex(choice.Options, option => option.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
}
