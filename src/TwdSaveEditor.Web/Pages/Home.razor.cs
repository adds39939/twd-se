using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Web.Components;
using TwdSaveEditor.Web.Services;
using TwdSaveEditor.Core.GameData;

namespace TwdSaveEditor.Web.Pages;

public partial class Home : IDisposable
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    private string ActiveTab { get; set; } = "decisions";
    private NewSaveDialog? _newSaveDialog;
    private bool _saving;

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

    private string ActiveTabTitle => ActiveTab switch
    {
        "decisions" => "Decisions",
        "resume" => "Resume Point",
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
