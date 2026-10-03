using System.Globalization;

namespace TwdSaveEditor.UI.Components.Icons;

public static class IconSize
{
    private const double DesignFontSize = 14;

    public static string Length(int size) =>
        (size / DesignFontSize).ToString("0.###", CultureInfo.InvariantCulture) + "rem";
}
