using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.UI.Components;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Pages;

public partial class Home : IDisposable
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public IJSRuntime JS { get; set; } = default!;

    private string ActiveTab { get; set; } = "decisions";
    private NewSaveDialog? _newSaveDialog;
    private bool _saving;
    private IChoiceAccessor? _accessor;
    private (SaveSlot? Slot, int Revision) _accessorFor;

    protected override void OnInitialized()
    {
        Editor.StateChanged += OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private IChoiceAccessor? GetAccessor()
    {
        if (Editor.SelectedSave is not { } slot)
        {
            return null;
        }

        var current = (slot, Editor.Revision(slot));
        if (current != _accessorFor)
        {
            _accessor = Editor.GetChoiceAccessor(slot);
            _accessorFor = current;
        }

        return _accessor;
    }

    private async Task ShowNewSaveDialog()
    {
        if (_newSaveDialog != null)
        {
            await _newSaveDialog.Show();
        }
    }

    private string TabClass(string tab) => ActiveTab == tab ? "active" : "";

    private string SaveLabel => Editor.DownloadsChanges ? "Download Changes" : "Save Changes";

    private string ActiveTabTitle => ActiveTab switch
    {
        "decisions" => "Decisions",
        "resume" => "Resume Point",
        "inventory" => "Inventory",
        "properties" => "Properties",
        _ => string.Empty
    };

    private async Task SaveChanges()
    {
        if (Editor.SelectedSave is null)
        {
            return;
        }

        _saving = true;
        await InvokeAsync(StateHasChanged);

        await Editor.SaveFile(Editor.SelectedSave);
        _saving = false;
        await InvokeAsync(StateHasChanged);
    }

    private async Task DiscardChanges()
    {
        if (Editor.SelectedSave is not { } slot
            || !await JS.InvokeAsync<bool>("confirm", $"Discard the unsaved changes to {slot.FileName} and reload it?"))
        {
            return;
        }

        await Editor.DiscardChanges(slot);
    }

    public void Dispose()
    {
        Editor.StateChanged -= OnStateChanged;
    }
}
