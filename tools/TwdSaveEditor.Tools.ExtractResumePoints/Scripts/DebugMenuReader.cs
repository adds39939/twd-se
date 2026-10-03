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
        var anchor = -1;
        var repeated = false;
        foreach (Match match in Button().Matches(text))
        {
            var title = match.Groups["title"].Value;
            if (!match.Groups["target"].Success)
            {
                repeated = title != group && entries.Any(existing => existing.Group == title);
                group = title;
                continue;
            }

            if (!match.Groups["script"].Success)
            {
                continue;
            }

            var command = match.Groups["command"].Value.Replace("\\\"", "\"");
            var entry = new MenuEntry(group, title.Trim(), match.Groups["script"].Value, [.. LogicSet().Matches(command).Select(flag => ReadFlag(flag, constants))]);
            if (!DialogTitle().IsMatch(entry.Title))
            {
                var identical = entries.FindIndex(existing => Identical(existing, entry));
                if (identical >= 0)
                {
                    anchor = identical;
                    continue;
                }

                var position = repeated ? After(entries, entry) : entries.Count;
                entries.Insert(position, entry);
                anchor = position;
                continue;
            }

            var known = entries.FindIndex(existing => Same(existing, entry));
            if (known >= 0)
            {
                anchor = known;
                continue;
            }

            while (anchor + 1 < entries.Count && entries[anchor + 1].Script.Equals(entries[anchor].Script, StringComparison.OrdinalIgnoreCase))
            {
                anchor++;
            }

            entries.Insert(++anchor, entry with { Title = SceneTitle(entry.Title) });
        }

        return entries;
    }

    private static int After(List<MenuEntry> entries, MenuEntry entry)
    {
        var sameScene = entries.FindLastIndex(existing => existing.Group == entry.Group && existing.Script.Equals(entry.Script, StringComparison.OrdinalIgnoreCase));
        var position = sameScene >= 0 ? sameScene : entries.FindLastIndex(existing => existing.Group == entry.Group);
        return position + 1;
    }

    private static bool Same(MenuEntry first, MenuEntry second) =>
        first.Script.Equals(second.Script, StringComparison.OrdinalIgnoreCase)
        && first.Flags.Select(flag => flag.Key).Order().SequenceEqual(second.Flags.Select(flag => flag.Key).Order());

    private static bool Identical(MenuEntry first, MenuEntry second) =>
        first.Script.Equals(second.Script, StringComparison.OrdinalIgnoreCase)
        && FlagValues(first).SequenceEqual(FlagValues(second));

    private static IEnumerable<string> FlagValues(MenuEntry entry) =>
        entry.Flags.Select(flag => $"{flag.Key}={flag.Value.ToJsonString()}").Order(StringComparer.Ordinal);

    private static string SceneTitle(string dialogTitle)
    {
        var name = DialogTitle().Match(dialogTitle).Groups["name"].Value.Replace('_', ' ');
        var words = Words().Replace(name, " $1");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
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

    [GeneratedRegex("DebugMenu_AddButton\\(\\s*\\d+\\s*,\\s*(?:\\d+\\s*,\\s*)?\"(?<title>[^\"]*)\"(?<target>\\s*,\\s*(?:\"(?<script>[^\"]*)\"|nil))?(?:\\s*,\\s*\"(?<command>(?:[^\"\\\\]|\\\\.)*)\")?\\s*\\)")]
    private static partial Regex Button();

    [GeneratedRegex("^env_(?<name>\\w+)\\.dlog")]
    private static partial Regex DialogTitle();

    [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
    private static partial Regex Words();

    [GeneratedRegex("LogicSet\\(\\s*(?:[\"'](?<key>[^\"']*)[\"']|(?<constant>\\w+))\\s*,\\s*(?<value>[^)]*?)\\s*\\)")]
    private static partial Regex LogicSet();
}
