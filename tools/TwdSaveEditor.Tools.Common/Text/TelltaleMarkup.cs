using System.Text.RegularExpressions;

namespace TwdSaveEditor.Tools.Common.Text;

public static partial class TelltaleMarkup
{
    public static string Strip(string text)
    {
        if (text.Length == 0)
            return text;

        var cleaned = TagPattern().Replace(text, "");
        cleaned = cleaned.Replace("^^", "").Replace("^", "");
        return TextFormat.Trim(WhitespacePattern().Replace(cleaned, " "));
    }

    [GeneratedRegex(@"\^[a-zA-Z]+:[^^ ]*\^")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"[\s\x1c-\x1f]+")]
    private static partial Regex WhitespacePattern();
}
