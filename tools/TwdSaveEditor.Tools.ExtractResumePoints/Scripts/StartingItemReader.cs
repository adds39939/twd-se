using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

public static partial class StartingItemReader
{
    public static List<StartingItem> Read(string script)
    {
        var items = new List<StartingItem>();
        var conditions = new Stack<string?>();
        foreach (var line in script.Split('\n').Select(line => line.Trim()))
        {
            if (Block().Match(line) is { Success: true } block)
            {
                conditions.Push(block.Groups["condition"].Success ? block.Groups["condition"].Value : null);
            }
            else if (line == "end" && conditions.Count > 0)
            {
                conditions.Pop();
            }
            else if ((line == "else" || line.StartsWith("elseif ", StringComparison.Ordinal)) && conditions.Count > 0 && conditions.Pop() is var _)
            {
                conditions.Push(line);
            }

            foreach (var item in InventoryCalls.Added(line))
            {
                items.Add(Create(item, [.. conditions.OfType<string>()]));
            }
        }

        return items;
    }

    private static StartingItem Create(string item, IReadOnlyList<string> conditions)
    {
        var requires = new List<string>();
        var unless = new List<string>();
        foreach (var condition in conditions)
        {
            var rest = Flag().Replace(condition, match =>
            {
                (match.Groups["not"].Success ? unless : requires).Add(match.Groups["key"].Value);
                return string.Empty;
            });

            if (rest.Replace("and", string.Empty, StringComparison.Ordinal).Trim().Length > 0)
            {
                throw new InvalidDataException($"Unsupported condition for the starting item {item}: {condition}");
            }
        }

        return new StartingItem(item, requires, unless);
    }

    [GeneratedRegex("^(?:if (?<condition>.+) then|(?:local )?function\\b.*|for\\b.* do|while\\b.* do|do)$")]
    private static partial Regex Block();

    [GeneratedRegex("(?<not>not\\s+)?LogicGet\\(\"(?<key>[^\"]+)\"\\)")]
    private static partial Regex Flag();
}
