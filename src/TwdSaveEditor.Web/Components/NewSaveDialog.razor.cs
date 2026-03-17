using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TwdSaveEditor.Web.Services;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.Web.Components;

public partial class NewSaveDialog
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    [Inject]
    public ISeasonRegistry Registry { get; set; } = default!;

    private ElementReference _dialogElement;
    private string _seasonKey = "s1";
    private int _episode = 1;
    private string _fileName = "wd1_saveslot1.bundle";
    private bool _creating;
    private Season? _selectedSeason;

    private static NewSaveDialog? _instance;

    protected override void OnInitialized()
    {
        _instance = this;
        _selectedSeason = SeasonInfo.FindSeason(_seasonKey);
    }

    public static void Show()
    {
        _instance?.ShowDialog();
    }

    private async void ShowDialog()
    {
        await JS.InvokeVoidAsync("eval", "document.getElementById('newSaveDialog')?.showModal()");
        StateHasChanged();
    }

    private async Task Close()
    {
        await JS.InvokeVoidAsync("eval", "document.getElementById('newSaveDialog')?.close()");
    }

    private void OnSeasonChanged(ChangeEventArgs e)
    {
        _seasonKey = e.Value?.ToString() ?? "s1";
        _selectedSeason = SeasonInfo.FindSeason(_seasonKey);
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
        var handler = Registry.Get(_seasonKey);
        var prefix = handler?.FilePrefix.TrimEnd('_') ?? "wd1";
        _fileName = $"{prefix}_saveslot1.bundle";
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
