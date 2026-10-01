using Microsoft.AspNetCore.Components;
using TwdSaveEditor.UI.Services;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.UI.Components;

public partial class PropertyEditor
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Inject]
    public PropertyNameDb NameDb { get; set; } = default!;

    [Parameter] public SaveSlot? Slot { get; set; }

    private static string TruncateValue(object? value)
    {
        if (value == null) return "(null)";
        var str = value.ToString() ?? "";
        return str.Length > 80 ? str[..80] + "..." : str;
    }
}
