using Microsoft.AspNetCore.Components;

namespace TwdSaveEditor.UI.Components;

public partial class WarningAlert
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
