using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Extensions;
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

    [Parameter]
    public EventCallback OnCreated { get; set; }

    private ElementReference _dialogElement;
    private string _seasonKey = "";
    private int _episode = 1;
    private int _slot;
    private string _fileName = "";
    private bool _creating;
    private ISeasonHandler? _selectedSeason;

    protected override void OnInitialized()
    {
        SelectSeason(Registry.All.FirstOrDefault()?.SeasonKey ?? "");
    }

    public async Task Show()
    {
        await UpdateFileName();
        await JS.InvokeVoidAsync("eval", "document.getElementById('newSaveDialog')?.showModal()");
        StateHasChanged();
    }

    private async Task Close()
    {
        await JS.InvokeVoidAsync("eval", "document.getElementById('newSaveDialog')?.close()");
    }

    private async Task OnSeasonChanged(ChangeEventArgs e)
    {
        SelectSeason(e.Value?.ToString() ?? _seasonKey);
        await UpdateFileName();
    }

    private void SelectSeason(string seasonKey)
    {
        _seasonKey = seasonKey;
        _selectedSeason = Registry.Get(seasonKey);
        _episode = 1;
    }

    private void OnEpisodeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var ep))
        {
            _episode = ep;
        }
    }

    private async Task UpdateFileName()
    {
        if (_selectedSeason == null)
        {
            return;
        }

        _slot = await Editor.NextFreeSlot(_selectedSeason);
        _fileName = _selectedSeason.SaveSlotFileName(_slot);
    }

    private bool OutsideGameSlots => _selectedSeason != null && _slot > _selectedSeason.SaveSlotCount;

    private async Task Create()
    {
        if (_selectedSeason == null)
        {
            return;
        }

        _creating = true;
        StateHasChanged();

        var created = await Editor.CreateNewSave(_selectedSeason, _episode);
        await Close();

        _creating = false;
        StateHasChanged();

        if (created != null)
        {
            await OnCreated.InvokeAsync();
        }
    }
}
