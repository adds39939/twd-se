using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Components;

public partial class SaveBrowser : IDisposable
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public IFileSystemService FileSystem { get; set; } = default!;

    [Inject]
    public ISeasonRegistry Registry { get; set; } = default!;

    [Parameter]
    public EventCallback OnNewSaveRequested { get; set; }

    private bool _isSupported = true;
    private bool _supportChecked;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isSupported = await FileSystem.IsSupported();
            _supportChecked = true;
            StateHasChanged();
            Editor.StateChanged += OnStateChanged;
        }
    }

    private async Task OpenDirectory()
    {
        var picked = await Editor.PickDirectory();
        if (picked)
        {
            await Editor.LoadDirectory();
        }
    }

    private async Task OnFilesUploaded(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles(50))
        {
            try
            {
                using var ms = new MemoryStream();
                await file.OpenReadStream(maxAllowedSize: 1024 * 1024 * 10).CopyToAsync(ms);
                var data = ms.ToArray();

                if (file.Name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
                {
                    Editor.Saves.Add(Editor.ReadBundle(data, file.Name));
                }
            }
            catch (Exception ex)
            {
                Editor.StatusMessage = $"Failed to load {file.Name}: {ex.Message}";
            }
        }
        Editor.NotifyStateChanged();
        StateHasChanged();
    }

    private async Task DownloadSelected()
    {
        if (Editor.SelectedSave == null) return;

        var slot = Editor.SelectedSave;
        var fileBytes = Editor.WriteBundle(slot);
        await FileSystem.DownloadFile(slot.FileName, fileBytes);
    }

    private void SelectSave(SaveSlot save)
    {
        Editor.SelectedSave = save;
        Editor.NotifyStateChanged();
    }

    private Task ShowNewSaveDialog() => OnNewSaveRequested.InvokeAsync();

    private void OnStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private string GetSeasonLabel(string key)
        => Registry.Get(key)?.ShortName ?? key.ToUpper();

    public void Dispose()
    {
        Editor.StateChanged -= OnStateChanged;
    }
}
