using Microsoft.AspNetCore.Components;
using TwdSaveEditor.UI.Configuration;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Layout;

public partial class MainLayout
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public AppInfo AppInfo { get; set; } = default!;
}
