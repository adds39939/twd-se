using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Model;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Components;

public partial class DecisionEditor
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public ISeasonRegistry Registry { get; set; } = default!;

    [Parameter] public SaveSlot? Slot { get; set; }
    [Parameter] public IChoiceAccessor? Accessor { get; set; }

    private string? _selectedImportSave;
    private Dictionary<string, int> _choiceStates = new();
    private List<(ISeasonHandler Season, List<IGrouping<int, ChoiceDefinition>> Episodes)>? _cachedTree;
    private string? _lastSlotFileName;
    private bool _showEndingPresets;

    protected override void OnParametersSet()
    {
        var currentFile = Slot?.FileName;
        if (currentFile != _lastSlotFileName)
        {
            _lastSlotFileName = currentFile;
            _showEndingPresets = false;
            _cachedTree = null;
        }

        RebuildCache();
    }

    private void RebuildCache()
    {
        _choiceStates.Clear();
        if (Slot == null || Accessor == null)
        {
            _cachedTree = null;
            return;
        }

        _cachedTree ??= GetRelevantSeasons(Slot.DetectedSeasonKey)
            .Select(s => (
                Season: s,
                Episodes: s.Choices
                    .GroupBy(c => c.Episode)
                    .OrderBy(g => g.Key)
                    .ToList()
            ))
            .Where(x => x.Episodes.Count > 0)
            .ToList();

        foreach (var (season, episodes) in _cachedTree)
        {
            foreach (var epGroup in episodes)
            {
                foreach (var choice in epGroup)
                {
                    _choiceStates[choice.ChoiceKey] = Accessor.DetectCurrentChoice(choice);
                }
            }
        }
    }

    private int GetChoiceState(string choiceKey)
        => _choiceStates.GetValueOrDefault(choiceKey, -1);

    private void OnChoiceChanged(ChoiceDefinition choice, ChangeEventArgs e)
    {
        if (Accessor == null) return;
        if (int.TryParse(e.Value?.ToString(), out var idx) && idx >= 0 && idx < choice.Options.Length)
        {
            Accessor.ApplyChoice(choice, idx);
            _choiceStates[choice.ChoiceKey] = idx;
            Editor.MarkModified();
            Editor.CascadeChoice(choice.ChoiceKey, choice.Options[idx].Value, Slot?.DetectedSeasonKey ?? "");
        }
    }

    private ISeasonHandler? CurrentSeason
        => Slot?.DetectedSeasonKey != null ? Registry.Get(Slot.DetectedSeasonKey) : null;

    private List<ISeasonHandler> GetRelevantSeasons(string? detectedKey)
    {
        if (detectedKey == null)
            return Registry.All.ToList();

        var includedKeys = Registry.Get(detectedKey)?.IncludedSeasonKeys ?? [];
        return includedKeys
            .Select(Registry.Get)
            .OfType<ISeasonHandler>()
            .ToList();
    }

    private List<SaveSlot> GetImportSources(IChoiceImporter importer)
        => Editor.Saves.Where(importer.CanImportFrom).ToList();

    private string GetSeasonNames(IEnumerable<SaveSlot> saves)
        => string.Join(" / ", saves
            .Select(s => s.DetectedSeasonKey != null ? Registry.Get(s.DetectedSeasonKey)?.Name : null)
            .OfType<string>()
            .Distinct());

    private (string SeasonKey, int Episode)? ExpandedGroup()
    {
        if (Slot == null || CurrentSeason is not { } current)
            return null;

        var inProgress = current is IResumePointHandler resume ? resume.GetResumeState(Slot).Episode : 1;
        return current.DecisionGroupOf(inProgress);
    }

    private void ImportChoices(IChoiceImporter importer)
    {
        if (_selectedImportSave == null || Slot == null) return;
        var source = Editor.Saves.FirstOrDefault(s => s.FileName == _selectedImportSave);
        if (source == null || !importer.CanImportFrom(source)) return;

        importer.ImportChoices(source, Slot);

        RebuildCache();
        Editor.MarkModified();
    }

    private void ApplyPreset(ChoicePreset preset)
    {
        if (Accessor == null) return;

        foreach (var selection in preset.Selections)
            Accessor.SetChoiceValue(selection.ChoiceKey, selection.Value);

        RebuildCache();
        Editor.MarkModified();
    }

    private static string GetCategoryBadgeClass(string category) => category switch
    {
        "Life/Death" => "badge-accent",
        "Relationship" => "badge-warning",
        _ => "",
    };
}
