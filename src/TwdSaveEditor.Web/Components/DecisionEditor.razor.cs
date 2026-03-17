using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Web.Services;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Web.Components;

public partial class DecisionEditor
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Parameter] public SaveSlot? Slot { get; set; }
    [Parameter] public IChoiceAccessor? Accessor { get; set; }

    private string? _selectedImportSave;

    private void OnChoiceChanged(ChoiceDefinition choice, ChangeEventArgs e)
    {
        if (Accessor == null) return;
        if (int.TryParse(e.Value?.ToString(), out var idx) && idx >= 0 && idx < choice.Options.Length)
        {
            Accessor.ApplyChoice(choice, idx);
            Editor.MarkModified();
            StateHasChanged();
        }
    }

    private static List<Season> GetRelevantSeasons(string? detectedKey)
    {
        if (detectedKey == null)
            return SeasonInfo.Seasons.ToList();

        // For S1 saves, show both S1 and 400 Days
        if (detectedKey == "s1")
            return SeasonInfo.Seasons
                .Where(s => s.Key is "s1" or "s1_400days")
                .ToList();

        return SeasonInfo.Seasons
            .Where(s => s.Key == detectedKey)
            .ToList();
    }

    private static bool ShouldExpandEpisode(string seasonKey, int episode, string? detectedKey)
    {
        if (seasonKey != detectedKey) return false;
        // Expand first episode by default
        return episode == 1;
    }

    private void ImportS1Choices()
    {
        if (_selectedImportSave == null || Slot?.Choices == null) return;
        var s1Save = Editor.Saves.FirstOrDefault(s => s.FileName == _selectedImportSave);
        if (s1Save?.Choices == null) return;

        var s1Accessor = new SaveAccessor(s1Save.Choices);
        var s2Accessor = new SaveAccessor(Slot.Choices);

        foreach (var (key, value) in s1Accessor.GetAllChoices())
        {
            s2Accessor.SetChoiceValue(key, value);
        }

        Editor.MarkModified();
        StateHasChanged();
    }

    private void ApplyPreset(string preset)
    {
        if (Slot?.ChoiceStats == null) return;
        var accessor = new ChoiceStatsAccessor(Slot);

        switch (preset)
        {
            case "louis":
                accessor.SetChoiceValue("follow_violet_louis", "louis");
                accessor.SetChoiceValue("violetlouis_saved", "louis");
                break;
            case "violet":
                accessor.SetChoiceValue("follow_violet_louis", "violet");
                accessor.SetChoiceValue("violetlouis_saved", "violet");
                break;
            case "trust_aj":
                accessor.SetChoiceValue("trusted_aj", "true");
                break;
        }

        Editor.MarkModified();
        StateHasChanged();
    }

    private static string GetCategoryBadgeClass(string category) => category switch
    {
        "Life/Death" => "badge-accent",
        "Relationship" => "badge-warning",
        _ => "",
    };
}
