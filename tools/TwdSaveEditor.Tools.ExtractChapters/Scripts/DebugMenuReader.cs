using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractChapters.Model;

namespace TwdSaveEditor.Tools.ExtractChapters.Scripts;

public static partial class DebugMenuReader
{
    public const string GameLogicAgent = "logic_game";
    public const string InventoryAgent = "logic_inventory_items";

    public static List<Chapter> Read(string script)
    {
        var constants = Constant().Matches(script).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
        var handlers = Handler().Matches(script).ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);

        var chapters = new List<Chapter>();
        var group = string.Empty;
        foreach (Match button in Button().Matches(script))
        {
            var title = button.Groups[1].Value;
            if (!button.Groups[2].Success)
            {
                if (title.Length > 0)
                    group = title;

                continue;
            }

            var name = button.Groups[2].Value;
            if (!handlers.TryGetValue(name, out var body) || !LoadScript().IsMatch(body))
                continue;

            chapters.Add(ReadChapter(name, title, group, body, constants));
        }

        return chapters;
    }

    public static List<string> ReadToggles(string script) =>
        [.. Toggle().Matches(script).Select(match => match.Groups[1].Value).Distinct()];

    private static Chapter ReadChapter(string handler, string title, string group, string body, Dictionary<string, string> constants)
    {
        var assignments = new List<Assignment>();
        var scripts = new List<string>();
        var conditional = false;

        foreach (var line in body.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            if (LogicAssignment().Match(line) is { Success: true } logic)
                assignments.Add(Assign(GameLogicAgent, logic.Groups[1].Value, logic.Groups[2].Value, constants));
            else if (LogicField().Match(line) is { Success: true } field)
                assignments.Add(Assign(GameLogicAgent, $"\"{field.Groups[1].Value}\"", field.Groups[2].Value, constants));
            else if (AgentProperty().Match(line) is { Success: true } property)
                assignments.Add(Assign(property.Groups[1].Value, property.Groups[2].Value, property.Groups[3].Value, constants));
            else if (InventoryItem().Match(line) is { Success: true } item)
                assignments.Add(Assign(InventoryAgent, item.Groups[1].Value, "1", constants));
            else if (LoadScript().Match(line) is { Success: true } load)
                scripts.Add(load.Groups[1].Value);
            else if (line.StartsWith("if ", StringComparison.Ordinal) || line == "else")
                conditional = true;
        }

        return new Chapter(handler, title, group, scripts.Distinct().Count() == 1 ? scripts[0] : null, assignments, conditional);
    }

    private static Assignment Assign(string agent, string key, string value, Dictionary<string, string> constants)
    {
        var name = Text(key, constants);
        var parsed = Value(value, constants);
        return new Assignment(agent, name ?? key, parsed, name == null || parsed == null ? $"{key} = {value}" : null);
    }

    private static string? Text(string expression, Dictionary<string, string> constants)
    {
        if (expression.Length >= 2 && expression[0] == '"' && expression[^1] == '"')
            return expression[1..^1];

        return constants.GetValueOrDefault(expression);
    }

    private static JsonNode? Value(string expression, Dictionary<string, string> constants)
    {
        if (bool.TryParse(expression, out var flag))
            return JsonValue.Create(flag);
        if (int.TryParse(expression, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return JsonValue.Create(number);

        return Text(expression, constants) is { } text ? JsonValue.Create(text) : null;
    }

    [GeneratedRegex("^local (k\\w+) = \"([^\"]*)\"$", RegexOptions.Multiline)]
    private static partial Regex Constant();

    [GeneratedRegex("^    (\\w+) = function\\(self, element\\)\\n(.*?)^    end,?$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex Handler();

    [GeneratedRegex("\\{\\s*(?:name = \"[^\"]*\",\\s*)?text = \"([^\"]*)\"(?:,\\s*handler = \"(\\w+)\")?\\s*\\}")]
    private static partial Regex Button();

    [GeneratedRegex("^Logic\\[(.+?)\\] = (.+)$")]
    private static partial Regex LogicAssignment();

    [GeneratedRegex("^Logic\\.(\\w+) = (.+)$")]
    private static partial Regex LogicField();

    [GeneratedRegex("^AgentSetProperty\\(\"([^\"]+)\", (.+?), (.+)\\)$")]
    private static partial Regex AgentProperty();

    [GeneratedRegex("^WDInventory_AddItem\\((.+?)\\)$")]
    private static partial Regex InventoryItem();

    [GeneratedRegex("ToggleTrueFalse\\(\"([^\"]+)\"\\)")]
    private static partial Regex Toggle();

    [GeneratedRegex("LoadScript\\(\"([^\"]+)\"\\)")]
    private static partial Regex LoadScript();
}
