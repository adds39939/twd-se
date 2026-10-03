using System.Globalization;
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

    private void SetString(StringValue property, ChangeEventArgs e)
    {
        property.Value = e.Value?.ToString() ?? string.Empty;
        Editor.MarkModified(Slot!);
    }

    private void SetInt(IntValue property, ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), CultureInfo.InvariantCulture, out var value))
        {
            property.Value = value;
            Editor.MarkModified(Slot!);
        }
    }

    private void SetBool(BoolValue property, ChangeEventArgs e)
    {
        property.Value = e.Value is true;
        Editor.MarkModified(Slot!);
    }

    private static string TruncateValue(object? value)
    {
        if (value == null)
        {
            return "(null)";
        }

        var str = value.ToString() ?? "";
        return str.Length > 80 ? str[..80] + "..." : str;
    }
}
