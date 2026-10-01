using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Components;

public partial class NewSaveDialog
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    [Inject]
    public ISeasonRegistry Registry { get; set; } = default!;

    private const int MaxSlots = 4;

    private ElementReference _dialogElement;
    private string _seasonKey = "";
    private int _episode = 1;
    private string _fileName = "";
    private bool _creating;
    private ISeasonHandler? _selectedSeason;

    protected override void OnInitialized()
    {
        SelectSeason(Registry.All.FirstOrDefault()?.SeasonKey ?? "");
    }

    public async Task Show()
    {
        UpdateFileName();
        await JS.InvokeVoidAsync("eval", "document.getElementById('newSaveDialog')?.showModal()");
        StateHasChanged();
    }

    private async Task Close()
    {
        await JS.InvokeVoidAsync("eval", "document.getElementById('newSaveDialog')?.close()");
    }

    private void OnSeasonChanged(ChangeEventArgs e)
    {
        SelectSeason(e.Value?.ToString() ?? _seasonKey);
    }

    private void SelectSeason(string seasonKey)
    {
        _seasonKey = seasonKey;
        _selectedSeason = Registry.Get(seasonKey);
        _episode = 1;
        UpdateFileName();
    }

    private void OnEpisodeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var ep))
            _episode = ep;
    }

    private void UpdateFileName()
    {
        if (_selectedSeason == null) return;

        var prefix = _selectedSeason.FilePrefix.TrimEnd('_');
        var names = Enumerable.Range(1, MaxSlots).Select(slot => $"{prefix}_saveslot{slot}.bundle").ToList();
        _fileName = names.FirstOrDefault(name => !Editor.Saves.Any(save => save.FileName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            ?? names[0];
    }

    private async Task Create()
    {
        if (string.IsNullOrWhiteSpace(_fileName)) return;

        _creating = true;
        StateHasChanged();

        var name = _fileName.Trim();
        if (!name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
            name += ".bundle";

        await Editor.CreateNewSave(_seasonKey, _episode, name);
        await Close();

        _creating = false;
        StateHasChanged();
    }
}
