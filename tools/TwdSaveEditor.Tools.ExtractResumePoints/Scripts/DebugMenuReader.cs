using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

public static partial class DebugMenuReader
{
    public static List<MenuEntry> Read(string text, IReadOnlyDictionary<string, string> constants)
    {
        var entries = new List<MenuEntry>();
        var group = string.Empty;
        foreach (Match match in Button().Matches(text))
        {
            var title = match.Groups["title"].Value;
            if (!match.Groups["target"].Success)
            {
                group = title;
                continue;
            }

            if (!match.Groups["script"].Success)
                continue;

            var command = match.Groups["command"].Value.Replace("\\\"", "\"");
            entries.Add(new MenuEntry(group, title, match.Groups["script"].Value, [.. LogicSet().Matches(command).Select(flag => ReadFlag(flag, constants))]));
        }

        return entries;
    }

    private static Flag ReadFlag(Match match, IReadOnlyDictionary<string, string> constants)
    {
        var key = match.Groups["key"].Success
            ? match.Groups["key"].Value
            : constants.TryGetValue(match.Groups["constant"].Value, out var name)
                ? name
                : throw new InvalidDataException($"The constant {match.Groups["constant"].Value} is not defined by the project scripts.");

        var value = match.Groups["value"].Value;
        return new Flag(key, value switch
        {
            "true" => JsonValue.Create(true),
            "false" => JsonValue.Create(false),
            _ when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            _ when value.Length > 1 && value[0] == '"' && value[^1] == '"' => JsonValue.Create(value[1..^1]),
            _ => throw new InvalidDataException($"Unsupported value {value} for {key}."),
        });
    }

    [GeneratedRegex("DebugMenu_AddButton\\(\\s*\\d+\\s*,\\s*\"(?<title>[^\"]*)\"(?<target>\\s*,\\s*(?:\"(?<script>[^\"]*)\"|nil))?(?:\\s*,\\s*\"(?<command>(?:[^\"\\\\]|\\\\.)*)\")?\\s*\\)")]
    private static partial Regex Button();

    [GeneratedRegex("LogicSet\\(\\s*(?:\"(?<key>[^\"]*)\"|(?<constant>\\w+))\\s*,\\s*(?<value>[^)]*?)\\s*\\)")]
    private static partial Regex LogicSet();
}
