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
    private (string SeasonKey, int Episode)? _expanded;
    private (SaveSlot? Slot, IChoiceAccessor? Accessor, int Revision) _shown;
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

        var shown = (Slot, Accessor, Slot == null ? 0 : Editor.Revision(Slot));
        if (shown != _shown)
        {
            _shown = shown;
            RebuildCache();
        }

        SelectImportSource();
    }

    private void SelectImportSource()
    {
        var sources = CurrentSeason is IChoiceImporter importer ? GetImportSources(importer) : [];
        if (sources.All(source => source.FileName != _selectedImportSave))
        {
            _selectedImportSave = sources.FirstOrDefault()?.FileName;
        }
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

        var choices = _cachedTree.SelectMany(season => season.Episodes).SelectMany(episode => episode).ToList();
        foreach (var (choice, state) in choices.Zip(Accessor.DetectCurrentChoices(choices)))
        {
            _choiceStates[choice.ChoiceKey] = state;
        }

        _expanded = ExpandedGroup();
    }

    private int GetChoiceState(string choiceKey)
        => _choiceStates.GetValueOrDefault(choiceKey, -1);

    private void OnChoiceChanged(ChoiceDefinition choice, int index)
    {
        if (Slot == null || Accessor == null || index >= choice.Options.Length)
        {
            return;
        }

        var value = index >= 0 ? choice.Options[index].Value : null;
        try
        {
            if (value == null)
            {
                Accessor.ClearChoiceValue(choice.ChoiceKey);
            }
            else
            {
                Accessor.ApplyChoice(choice, index);
            }
        }
        catch (InvalidOperationException ex)
        {
            Editor.ShowError(ex.Message);
            return;
        }
        finally
        {
            _choiceStates[choice.ChoiceKey] = Accessor.DetectCurrentChoice(choice);
        }

        Editor.MarkModified(Slot);
        Editor.CascadeChoice(choice.ChoiceKey, value, Slot.DetectedSeasonKey ?? "");
    }

    private ISeasonHandler? CurrentSeason
        => Slot?.DetectedSeasonKey != null ? Registry.Get(Slot.DetectedSeasonKey) : null;

    private List<ISeasonHandler> GetRelevantSeasons(string? detectedKey)
    {
        if (detectedKey == null)
        {
            return Registry.All.ToList();
        }

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
        {
            return null;
        }

        var inProgress = current is IResumePointHandler resume ? resume.GetResumeState(Slot).Episode : 1;
        return current.DecisionGroupOf(inProgress);
    }

    private void ImportChoices(IChoiceImporter importer)
    {
        if (_selectedImportSave == null || Slot == null)
        {
            return;
        }

        var source = Editor.Saves.FirstOrDefault(s => s.FileName == _selectedImportSave);
        if (source == null || !importer.CanImportFrom(source))
        {
            return;
        }

        importer.ImportChoices(source, Slot);
        Editor.MarkModified(Slot);
    }

    private void ApplyPreset(ChoicePreset preset)
    {
        if (Slot == null || Accessor == null)
        {
            return;
        }

        foreach (var selection in preset.Selections)
        {
            Accessor.SetChoiceValue(selection.ChoiceKey, selection.Value);
        }

        Editor.MarkModified(Slot);
    }

    private static string GetCategoryBadgeClass(string category) => category switch
    {
        "Life/Death" => "badge-accent",
        "Relationship" => "badge-warning",
        _ => "",
    };
}
