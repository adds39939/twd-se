using Microsoft.AspNetCore.Components;
using TwdSaveEditor.UI.Configuration;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Layout;

public partial class MainLayout : IDisposable
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public AppInfo AppInfo { get; set; } = default!;

    protected override void OnInitialized()
    {
        Editor.StateChanged += OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Editor.StateChanged -= OnStateChanged;
    }
}
