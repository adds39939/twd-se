using Microsoft.AspNetCore.Components;
using TwdSaveEditor.UI.Model;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Components;

public partial class ToastContainer : IDisposable
{
    [Inject] public SaveEditorService Editor { get; set; } = default!;

    private readonly List<Toast> _toasts = [];

    protected override void OnInitialized()
    {
        Editor.OnNotification += ShowToast;
    }

    private async void ShowToast(string message, string type)
    {
        var toast = new Toast(Guid.NewGuid(), message, type);
        _toasts.Add(toast);
        await InvokeAsync(StateHasChanged);
        _ = Task.Delay(5000).ContinueWith(_ =>
        {
            _toasts.Remove(toast);
            InvokeAsync(StateHasChanged);
        });
    }

    private void Dismiss(Toast toast)
    {
        _toasts.Remove(toast);
    }

    public void Dispose()
    {
        Editor.OnNotification -= ShowToast;
    }
}
