using System.Text;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.ValidateSaves.Lua;

public static partial class LuaStrings
{
    private const int MinimumLength = 4;

    public static List<string> FromSource(ReadOnlySpan<byte> lua) =>
        Identifier().Matches(TextFormat.DecodeUtf8(lua)).Select(match => match.Value).ToList();

    public static List<string> FromBytecode(ReadOnlySpan<byte> data)
    {
        var strings = new List<string>();
        var current = new StringBuilder();
        foreach (var b in data)
        {
            if (b is >= 0x20 and <= 0x7E)
            {
                current.Append((char)b);
                continue;
            }

            if (current.Length >= MinimumLength)
                strings.Add(current.ToString());

            current.Clear();
        }

        if (current.Length >= MinimumLength)
            strings.Add(current.ToString());

        return strings;
    }

    [GeneratedRegex("[A-Za-z_][A-Za-z0-9_]+")]
    private static partial Regex Identifier();
}
