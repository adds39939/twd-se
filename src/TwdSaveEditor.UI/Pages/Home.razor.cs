using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.UI.Components;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Pages;

public partial class Home : IDisposable
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    private string ActiveTab { get; set; } = "decisions";
    private NewSaveDialog? _newSaveDialog;
    private bool _saving;
    private IChoiceAccessor? _cachedAccessor;
    private string? _cachedAccessorSlot;

    protected override void OnInitialized()
    {
        Editor.StateChanged += OnStateChanged;
    }

    private void OnStateChanged()
    {
        _cachedAccessor = null;
        _cachedAccessorSlot = null;
        InvokeAsync(StateHasChanged);
    }

    private IChoiceAccessor? GetAccessor()
    {
        if (Editor.SelectedSave == null) return null;

        var slotFile = Editor.SelectedSave.FileName;
        if (slotFile == _cachedAccessorSlot && _cachedAccessor != null)
            return _cachedAccessor;

        _cachedAccessor = Editor.GetChoiceAccessor(Editor.SelectedSave);
        _cachedAccessorSlot = slotFile;
        return _cachedAccessor;
    }

    private async Task ShowNewSaveDialog()
    {
        if (_newSaveDialog != null)
            await _newSaveDialog.Show();
    }

    private string TabClass(string tab) => ActiveTab == tab ? "active" : "";

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

    public void Dispose()
    {
        Editor.StateChanged -= OnStateChanged;
    }
}
