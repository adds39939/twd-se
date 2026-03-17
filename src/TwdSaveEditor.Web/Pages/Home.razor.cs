using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Web.Components;
using TwdSaveEditor.Web.Services;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.Web.Pages;

public partial class Home : IDisposable
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    private string ActiveTab { get; set; } = "decisions";
    private NewSaveDialog? _newSaveDialog;

    protected override void OnInitialized()
    {
        Editor.StateChanged += OnStateChanged;
    }

    private void OnStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private IChoiceAccessor? GetAccessor()
    {
        return Editor.SelectedSave != null ? Editor.GetChoiceAccessor(Editor.SelectedSave) : null;
    }

    private string TabClass(string tab) => ActiveTab == tab ? "active" : "";

    public void Dispose()
    {
        Editor.StateChanged -= OnStateChanged;
    }
}
