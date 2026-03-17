using Microsoft.AspNetCore.Components;
using TwdSaveEditor.Web.Services;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Web.Components;

public partial class PropertyEditor
{
    [Inject]
    public SaveEditorService Editor { get; set; } = default!;

    [Parameter] public SaveSlot? Slot { get; set; }

    private readonly PropertyNameDb _nameDb = PropertyNameDb.CreateDefault();

    private static string TruncateValue(object? value)
    {
        if (value == null) return "(null)";
        var str = value.ToString() ?? "";
        return str.Length > 80 ? str[..80] + "..." : str;
    }
}
