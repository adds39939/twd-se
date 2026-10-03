using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.UI.Formatting;
using TwdSaveEditor.UI.Model;
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

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    [Parameter]
    public EventCallback OnNewSaveRequested { get; set; }

    private const int MaxUploadedFiles = 500;
    private const long MaxUploadedFileSize = 1024 * 1024 * 20;

    private readonly Dictionary<SaveSlot, (int Revision, SaveSummary Summary)> _summaries = [];
    private bool _isSupported = true;
    private bool _supportChecked;

    private bool IsBusy => Editor.IsLoading || !RendererInfo.IsInteractive;

    private bool UploadsFiles => _supportChecked && !_isSupported;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isSupported = await FileSystem.IsSupported();
            _supportChecked = true;
            StateHasChanged();
            Editor.StateChanged += OnStateChanged;
            if (_isSupported)
            {
                await Editor.RestoreDirectory();
            }
        }
    }

    private async Task OpenDirectory()
    {
        if (!await ConfirmDiscardChanges())
        {
            return;
        }

        var picked = await Editor.PickDirectory();
        if (picked)
        {
            await Editor.LoadDirectory();
        }
    }

    private async Task ReopenDirectory()
    {
        if (await Editor.ReopenDirectory())
        {
            await Editor.LoadDirectory();
        }
    }

    private async Task ReloadDirectory()
    {
        if (await ConfirmDiscardChanges())
        {
            await Editor.ReloadDirectory();
        }
    }

    private async Task OnFilesUploaded(InputFileChangeEventArgs e)
    {
        if (!await ConfirmDiscardChanges())
        {
            return;
        }

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

    private async Task<bool> ConfirmDiscardChanges()
    {
        if (!Editor.HasUnsavedChanges)
        {
            return true;
        }

        var names = string.Join(", ", Editor.ModifiedSaves.Select(save => save.FileName));
        return await JS.InvokeAsync<bool>("confirm", $"Unsaved changes to {names} will be lost. Continue?");
    }

    private void SelectSave(SaveSlot save)
    {
        Editor.SelectedSave = save;
        Editor.NotifyStateChanged();
    }

    private Task ShowNewSaveDialog() => OnNewSaveRequested.InvokeAsync();

    private void OnStateChanged()
    {
        foreach (var removed in _summaries.Keys.Except(Editor.Saves).ToList())
        {
            _summaries.Remove(removed);
        }

        InvokeAsync(StateHasChanged);
    }

    private SaveSummary Summary(SaveSlot save)
    {
        var revision = Editor.Revision(save);
        if (!_summaries.TryGetValue(save, out var cached) || cached.Revision != revision)
        {
            cached = (revision, Summarize(save));
            _summaries[save] = cached;
        }

        return cached.Summary;
    }

    private SaveSummary Summarize(SaveSlot save)
    {
        var season = save.DetectedSeasonKey != null ? Registry.Get(save.DetectedSeasonKey) : null;
        var slot = SlotNumber().Match(save.FileName) is { Success: true } match ? $"Slot {match.Groups[1].Value}" : null;
        var title = string.Join(" · ", new[] { season?.Name, slot }.OfType<string>());
        if (save.Metadata == null)
        {
            return new SaveSummary(title, "The save cannot be read", null, true);
        }

        if (season is not IResumePointHandler resume)
        {
            return new SaveSummary(title, string.Empty, null, save.EventLogDamaged);
        }

        var state = resume.GetResumeState(save);
        var episode = ResumeLabels.Episode(resume, state.Episode);
        var position = state switch
        {
            { SeasonFinished: true } => "Season finished",
            { CheckpointDamaged: true } => $"{episode} · checkpoint damaged",
            { StartsFromBeginning: true } => $"{episode} · from the beginning",
            _ => $"{episode} · {ResumeLabels.Checkpoint(state.Checkpoint)}",
        };
        return new SaveSummary(title, position, ResumeLabels.Saved(state.SavedAt), state.CheckpointDamaged || save.EventLogDamaged);
    }

    [GeneratedRegex(@"saveslot(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SlotNumber();

    public void Dispose()
    {
        Editor.StateChanged -= OnStateChanged;
    }
}
