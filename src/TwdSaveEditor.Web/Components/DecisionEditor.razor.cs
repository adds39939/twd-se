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

    private void OnChoiceChanged(ChoiceDefinition choice, ChangeEventArgs e)
    {
        if (Accessor == null) return;
        if (int.TryParse(e.Value?.ToString(), out var idx) && idx >= 0 && idx < choice.Options.Length)
        {
            Accessor.ApplyChoice(choice, idx);
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

    private static string GetCategoryBadgeClass(string category) => category switch
    {
        "Life/Death" => "badge-accent",
        "Relationship" => "badge-warning",
        _ => "",
    };
}
