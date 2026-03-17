using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TwdSaveEditor.Web.Services;
using TwdSaveEditor.Core.Binary;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Web.Components;

public partial class SaveBrowser : IDisposable
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public FileSystemService FileSystem { get; set; } = default!;

    [Inject]
    public ISeasonRegistry Registry { get; set; } = default!;

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
                    var slot = BundleReader.Read(data, file.Name);
                    var handler = Registry.DetectFromFileName(file.Name);
                    slot.DetectedSeasonKey = handler?.SeasonKey;
                    Editor.Saves.Add(slot);
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
        var fileBytes = BundleWriter.Write(slot);
        await FileSystem.DownloadFile(slot.FileName, fileBytes);
    }

    private void SelectSave(SaveSlot save)
    {
        Editor.SelectedSave = save;
        Editor.NotifyStateChanged();
    }

    private async Task ShowNewSaveDialog()
    {
        // Find the NewSaveDialog component via cascading parameter or direct reference
        // We'll trigger it via JS or a shared state approach
        NewSaveDialog.Show();
    }

    private void OnStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private static string GetSeasonLabel(string key) => key switch
    {
        "s1" => "S1",
        "s1_400days" => "400D",
        "s2" => "S2",
        "michonne" => "M",
        "s3" => "S3",
        "s4" => "S4",
        _ => key.ToUpper(),
    };

    public void Dispose()
    {
        Editor.StateChanged -= OnStateChanged;
    }
}
