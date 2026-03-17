using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Web.Services;

namespace TwdSaveEditor.Web.Layout;

public partial class MainLayout
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;
}
