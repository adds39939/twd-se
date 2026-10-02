using System.Globalization;
using TwdSaveEditor.Tools.Common.Meta;

namespace TwdSaveEditor.Tools.Common.Inventory;

public static class ItemValue
{
    public static bool Held(MetaNode value) => value is MetaScalar scalar && scalar.Value switch
    {
        bool flag => flag,
        string text => text.Length > 0,
        IConvertible number => number.ToDouble(CultureInfo.InvariantCulture) > 0,
        _ => false,
    };
}
