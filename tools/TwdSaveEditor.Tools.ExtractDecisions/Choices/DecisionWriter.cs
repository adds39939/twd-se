using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractDecisions.Model;

namespace TwdSaveEditor.Tools.ExtractDecisions.Choices;

public static partial class DecisionWriter
{
    private const string TitleSeparator = " - ";
    private const string DefaultCategory = "Other";
    private const string StoryCategory = "Story";
    private const string StatisticsCategory = "Statistics";

    public static string Title(Decision decision)
    {
        if (decision.Logic != null)
        {
            return decision.Logic.Name[(decision.Logic.Name.IndexOf(TitleSeparator, StringComparison.Ordinal) + TitleSeparator.Length)..];
        }

        return decision.RandomizerId != null
            ? Words().Replace(decision.RandomizerId, " $1").Trim()
            : decision.Stat!.Description.TrimEnd('?');
    }

    public static string Key(Decision decision, int season) =>
        decision.Logic?.Name ?? $"Episode {season}0{decision.Episode}{TitleSeparator}{Title(decision)}";

    public static List<(string Value, string Label)> Options(Decision decision)
    {
        var options = new List<(string Value, string Label)>();
        for (var index = 0; index < decision.Options.Count; index++)
        {
            var option = decision.Options[index];
            var label = Label(decision, option, index);
            var value = decision.Logic is { IsMap: false }
                ? (option.LogicValue != null).ToString().ToLowerInvariant()
                : option.LogicValue?.ToLowerInvariant() ?? Slug(label);

            while (options.Any(existing => existing.Value == value))
            {
                value += "_";
            }

            options.Add((value, label));
        }

        return options;
    }

    public static JsonObject Choice(Decision decision, int season, bool linked) => new()
    {
        ["seasonKey"] = $"s{season}",
        ["episode"] = decision.Episode,
        ["description"] = decision.Stat?.Description ?? Title(decision) + "?",
        ["choiceKey"] = Key(decision, season),
        ["category"] = Category(decision, linked),
        ["options"] = new JsonArray([.. Options(decision).Select(option => new JsonObject { ["label"] = option.Label, ["value"] = option.Value })]),
    };

    public static JsonObject Nodes(Decision decision, int season)
    {
        var options = Options(decision);
        return new JsonObject
        {
            ["choiceKey"] = Key(decision, season),
            ["logicKey"] = decision.Logic?.Name,
            ["readFrom"] = decision.Logic?.ReadFrom ?? 0,
            ["logicIsText"] = decision.Logic?.IsMap ?? false,
            ["requires"] = new JsonArray([.. decision.Requires.Select(node => JsonValue.Create(node))]),
            ["options"] = new JsonArray([.. decision.Options.Select((option, index) => new JsonObject
            {
                ["value"] = options[index].Value,
                ["logicValue"] = option.LogicValue,
                ["nodes"] = new JsonArray([.. option.Nodes.Select(node => JsonValue.Create(node))]),
            })]),
        };
    }

    private static string Category(Decision decision, bool linked)
    {
        if (!linked)
        {
            return decision.Logic != null ? StoryCategory : StatisticsCategory;
        }

        return decision.Stat == null ? DefaultCategory : Capitalise(decision.Stat.Category.ToLowerInvariant());
    }

    private static string Label(Decision decision, DecisionOption option, int index)
    {
        if (option.Label != null)
        {
            return PlayerText().Match(option.Label) is { Success: true } match ? Capitalise(match.Groups[1].Value) : option.Label;
        }

        if (decision.Logic is { IsMap: true })
        {
            return option.LogicValue ?? "None of these";
        }

        if (decision.Logic != null)
        {
            return option.LogicValue != null ? "Yes" : "No";
        }

        return $"Option {index + 1}";
    }

    private static string Slug(string label) =>
        string.Join('_', NonWord().Split(label.ToLowerInvariant()).Where(word => word.Length > 0).Take(5));

    private static string Capitalise(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    [GeneratedRegex("([A-Z])")]
    private static partial Regex Words();

    [GeneratedRegex(@"^You and [\d.]+ ?% of players (.*?)\.?$")]
    private static partial Regex PlayerText();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonWord();
}
