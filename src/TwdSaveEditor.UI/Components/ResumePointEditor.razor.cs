using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Components;

public partial class ResumePointEditor
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public ISeasonRegistry Registry { get; set; } = default!;

    [Parameter] public SaveSlot? Slot { get; set; }

    private int _playtime;
    private string _autosaveFile = "";

    private string _episodeId = "";
    private string _originalEpisodeId = "";
    private string _sceneName = "";

    private ISeasonHandler? _seasonHandler;
    private IReadOnlyList<string> _availableScenes = [];
    private HashSet<string> _knownEpisodeIds = [];

    public bool IsAutosave =>
        Slot != null && Path.GetFileName(Slot.FileName).StartsWith('_');

    public bool EpisodeChanged => _episodeId != _originalEpisodeId;

    protected override void OnParametersSet()
    {
        if (Slot?.Metadata == null) return;

        _seasonHandler = Slot.DetectedSeasonKey != null
            ? Registry.Get(Slot.DetectedSeasonKey)
            : null;

        if (IsAutosave)
        {
            LoadAutosaveFields();
            BuildKnownEpisodeIds();
            RefreshAvailableScenes();
        }
        else
        {
            LoadSlotFields();
        }
    }

    private void LoadSlotFields()
    {
        var props = Slot!.Metadata!.AllProperties;

        var playtimeProp = props.FirstOrDefault(p => p.KeySymbol.Value == 0x7C725227A47FD1BA);
        if (playtimeProp?.Value is IntValue ptv)
            _playtime = ptv.Value;

        var autosaveProp = props.FirstOrDefault(p => p.KeySymbol.Value == 0xF235E9FCE9562E01);
        if (autosaveProp?.Value is StringValue asv)
            _autosaveFile = asv.Value;
    }

    private void LoadAutosaveFields()
    {
        var props = Slot!.Metadata!.AllProperties;

        var epProp = props.FirstOrDefault(p => p.KeySymbol.Value == AutosaveHashes.EpisodeId);
        if (epProp?.Value is StringValue epv)
        {
            _episodeId = epv.Value;
            _originalEpisodeId = epv.Value;
        }

        var sceneProp = props.FirstOrDefault(p => p.KeySymbol.Value == AutosaveHashes.SceneName);
        if (sceneProp?.Value is StringValue snv)
            _sceneName = snv.Value;
    }

    private void BuildKnownEpisodeIds()
    {
        _knownEpisodeIds = [];
        if (_seasonHandler != null)
        {
            foreach (var ep in _seasonHandler.Episodes)
                _knownEpisodeIds.Add(_seasonHandler.GetEpisodeId(ep.Number));
        }
    }

    private void RefreshAvailableScenes()
    {
        _availableScenes = string.IsNullOrEmpty(_episodeId)
            ? []
            : Registry.GetScenes(_episodeId);
    }

    private static string FormatSceneName(string scene)
    {
        var name = scene.StartsWith("adv_") ? scene[4..] : scene;
        var chars = new List<char>();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                chars.Add(' ');
            chars.Add(i == 0 ? char.ToUpper(name[i]) : name[i]);
        }
        return new string(chars.ToArray()).Replace('_', ' ').Trim();
    }

    private void OnPlaytimeChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var minutes)) return;
        _playtime = minutes;
        SetPropertyInt(0x7C725227A47FD1BA, minutes);
        Editor.MarkModified();
    }

    private void OnAutosaveFileChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString() ?? "";
        _autosaveFile = val;
        SetPropertyString(0xF235E9FCE9562E01, val);
        Editor.MarkModified();
    }

    private void OnEpisodeIdChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString() ?? "";
        _episodeId = val;
        SetPropertyString(AutosaveHashes.EpisodeId, val);
        RefreshAvailableScenes();

        if (!string.IsNullOrEmpty(_sceneName) && !_availableScenes.Contains(_sceneName))
        {
            _sceneName = _availableScenes.Count > 0 ? _availableScenes[0] : "";
            SetPropertyString(AutosaveHashes.SceneName, _sceneName);
        }

        if (Slot != null)
            Slot.EpisodeChanged = EpisodeChanged;

        Editor.MarkModified();
    }

    private void OnSceneNameChanged(ChangeEventArgs e)
    {
        var val = e.Value?.ToString() ?? "";
        _sceneName = val;
        SetPropertyString(AutosaveHashes.SceneName, val);
        Editor.MarkModified();
    }

    private void SetPropertyString(ulong hash, string value)
    {
        if (Slot?.Metadata == null) return;

        var prop = Slot.Metadata.AllProperties.FirstOrDefault(p => p.KeySymbol.Value == hash);
        if (prop?.Value is StringValue sv)
        {
            sv.Value = value;
        }
        else
        {
            var strSymbol = Symbol.FromString("String");
            var group = Slot.Metadata.TypeGroups.FirstOrDefault(g => g.TypeSymbol == strSymbol);
            if (group == null)
            {
                group = new TypeGroup(strSymbol);
                Slot.Metadata.TypeGroups.Add(group);
            }
            group.Properties.Add(new Property(new Symbol(hash), new StringValue(value)));
        }
    }

    private void SetPropertyInt(ulong hash, int value)
    {
        if (Slot?.Metadata == null) return;

        var prop = Slot.Metadata.AllProperties.FirstOrDefault(p => p.KeySymbol.Value == hash);
        if (prop?.Value is IntValue iv)
        {
            iv.Value = value;
        }
        else
        {
            var int32Symbol = Symbol.FromString("int32");
            var group = Slot.Metadata.TypeGroups.FirstOrDefault(g => g.TypeSymbol == int32Symbol);
            if (group == null)
            {
                group = new TypeGroup(int32Symbol);
                Slot.Metadata.TypeGroups.Add(group);
            }
            group.Properties.Add(new Property(new Symbol(hash), new IntValue(value)));
        }
    }
}
