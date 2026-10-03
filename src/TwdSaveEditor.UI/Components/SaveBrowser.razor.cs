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

    private const int MaxUploadedFiles = 500;
    private const long MaxUploadedFileSize = 1024 * 1024 * 20;

    private bool _isSupported = true;
    private bool _supportChecked;

    private bool IsBusy => Editor.IsLoading || !RendererInfo.IsInteractive;

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
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in e.GetMultipleFiles(MaxUploadedFiles))
        {
            try
            {
                using var ms = new MemoryStream();
                await file.OpenReadStream(maxAllowedSize: MaxUploadedFileSize).CopyToAsync(ms);
                files[file.Name] = ms.ToArray();
            }
            catch (Exception ex)
            {
                Editor.StatusMessage = $"Failed to load {file.Name}: {ex.Message}";
            }
        }

        await Editor.LoadFiles(files);
        StateHasChanged();
    }

    private async Task DownloadSelected()
    {
        if (Editor.SelectedSave == null)
        {
            return;
        }

        var slot = Editor.SelectedSave;
        foreach (var file in Editor.BuildFiles(slot))
        {
            await FileSystem.DownloadFile(file.Name, file.Data);
        }

        if (slot.ObsoleteFileNames.Count > 0)
        {
            Editor.StatusMessage = $"Delete {string.Join(", ", slot.ObsoleteFileNames)} from your save folder before playing.";
        }

        Editor.NotifyStateChanged();
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
