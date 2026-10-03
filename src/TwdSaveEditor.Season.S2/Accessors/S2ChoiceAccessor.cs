using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.Season.S1.Persistence;
using TwdSaveEditor.Season.S2.Decisions;
using TwdSaveEditor.Season.S2.Saves;

namespace TwdSaveEditor.Season.S2.Accessors;

public sealed class S2ChoiceAccessor(SaveSlot slot) : IChoiceAccessor
{
    private readonly S2EventLogEditor _log = new(slot);

    public int DetectCurrentChoice(ChoiceDefinition choice) => IndexOf(choice, GetChoiceValue(choice.ChoiceKey));

    public IReadOnlyList<int> DetectCurrentChoices(IReadOnlyList<ChoiceDefinition> choices)
    {
        var present = _log.Nodes();
        return [.. choices.Select(choice => IndexOf(choice, ValueOf(choice.ChoiceKey, decision => S2EventLogEditor.GetValue(decision, present))))];
    }

    public void ApplyChoice(ChoiceDefinition choice, int optionIndex) =>
        SetChoiceValue(choice.ChoiceKey, choice.Options[optionIndex].Value);

    public string? GetChoiceValue(string choiceKey) => ValueOf(choiceKey, _log.GetValue);

    public void SetChoiceValue(string choiceKey, string value)
    {
        if (IsImportedKey(choiceKey))
        {
            if (slot.Choices == null)
            {
                throw new InvalidOperationException("Cannot set choice value: no choices PropertySet loaded.");
            }

            slot.Choices.SetString(choiceKey, value.ToLowerInvariant());
            return;
        }

        if (S2DecisionCatalog.Find(choiceKey) is not { } decision || decision.Find(value) is not { } option)
        {
            return;
        }

        DialogLogFiles.EnsureLog(slot);
        _log.SetValue(decision, option);
        UpdateSavedLogic(decision, option);
    }

    public void ClearChoiceValue(string choiceKey)
    {
        if (IsImportedKey(choiceKey))
        {
            slot.Choices?.Remove(Symbol.FromString(choiceKey));
            return;
        }

        if (slot.EventLog == null || S2DecisionCatalog.Find(choiceKey) is not { } decision)
        {
            return;
        }

        _log.Clear(decision);
        UpdateSavedLogic(decision, null);
    }

    private void UpdateSavedLogic(S2Decision decision, S2DecisionOption? option)
    {
        if (decision.LogicKey == null)
        {
            return;
        }

        foreach (var save in slot.Checkpoints)
        {
            if (save.FindFile(S2SlotFiles.LogicGameProperties) is not { } logic || !BundleReader.TryParseProperties(logic))
            {
                continue;
            }

            switch (logic.Properties!.Find(decision.LogicKey)?.Value)
            {
                case BoolValue flag when !decision.LogicIsText:
                    flag.Value = option?.LogicValue != null;
                    save.Modified = true;
                    break;
                case StringValue text when decision.LogicIsText:
                    text.Value = option?.LogicValue ?? string.Empty;
                    save.Modified = true;
                    break;
            }
        }
    }

    private string? ValueOf(string choiceKey, Func<S2Decision, string?> valueOf)
    {
        if (IsImportedKey(choiceKey))
        {
            return slot.Choices?.GetString(choiceKey);
        }

        return S2DecisionCatalog.Find(choiceKey) is { } decision ? valueOf(decision) : null;
    }

    private static int IndexOf(ChoiceDefinition choice, string? value) =>
        value == null ? -1 : Array.FindIndex(choice.Options, option => option.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static bool IsImportedKey(string choiceKey) => S1ChoiceCatalog.PersistentEpisode(choiceKey) != null;
}
