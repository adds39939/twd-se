using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractDecisions.Model;
using TwdSaveEditor.Tools.ExtractDecisions.Scripts;

namespace TwdSaveEditor.Tools.ExtractDecisions.Choices;

public static partial class ExpressionDecisions
{
    private const string TitleSeparator = " - ";
    private const string EpisodePrefix = "Episode ";
    private const string StoryCategory = "Story";
    private const string StatisticsCategory = "Statistics";
    private const int EpisodeBase = 100;
    private const int SlugWords = 5;
    private const string Never = "f";

    public static List<DecisionRow> Build(List<StatChoice> stats, List<LogicKey> logic, int season)
    {
        var rows = stats.Select(stat =>
        {
            var options = Distinct([.. stat.Options.Select(option => new RowOption(Slug(Label(option.Text)), Label(option.Text), option.Expression.Trim()))]);
            var title = TelltaleMarkup.Strip(stat.Description) is { Length: > 0 } description ? description : options[0].Label + "?";
            return new DecisionRow
            {
                Episode = stat.Episode,
                Key = $"{EpisodePrefix}{season}0{stat.Episode}{TitleSeparator}{title.TrimEnd('?')}",
                Description = title,
                Options = options,
            };
        }).ToList();

        var own = new List<DecisionRow>();
        foreach (var definitions in logic.GroupBy(key => key.Name))
        {
            var covering = definitions.Select(key => rows.FirstOrDefault(row => Covers(row, key))).OfType<DecisionRow>().ToList();
            if (covering.Count > 0)
            {
                covering.ForEach(row => row.Story = true);
                continue;
            }

            var options = definitions.OrderBy(key => key.ReadFrom).SelectMany(Options).DistinctBy(option => option.Value).ToList();
            if (options.Any(option => option.Expression.Length > 0 && !Node().IsMatch(option.Expression)))
                continue;

            var name = definitions.Key;
            var title = name[(name.IndexOf(TitleSeparator, StringComparison.Ordinal) + TitleSeparator.Length)..];
            own.Add(new DecisionRow
            {
                Episode = EpisodeOf(name, season),
                Key = name,
                Description = title.TrimEnd('?') + "?",
                Story = true,
                Options = options,
            });
        }

        return [.. rows.Concat(own).OrderBy(row => row.Episode)];
    }

    public static LogicKey WithoutBracedNodes(LogicKey key) => key with
    {
        Values =
        [
            .. key.Values.Select(value =>
            {
                var expression = Braced().Replace(value.Expression, Never);
                return (value.Value, NodeIds.In(expression), expression);
            }),
        ],
    };

    public static JsonObject Choice(DecisionRow row, string seasonKey) => new()
    {
        ["seasonKey"] = seasonKey,
        ["episode"] = row.Episode,
        ["description"] = row.Description,
        ["choiceKey"] = row.Key,
        ["category"] = row.Story ? StoryCategory : StatisticsCategory,
        ["options"] = new JsonArray([.. row.Options.Select(option => new JsonObject { ["label"] = option.Label, ["value"] = option.Value })]),
    };

    public static JsonObject Decisions(List<DecisionRow> rows, List<LogicKey> logic) => new()
    {
        ["decisions"] = new JsonArray([.. rows.Select(row => new JsonObject
        {
            ["choiceKey"] = row.Key,
            ["episode"] = row.Episode,
            ["options"] = new JsonArray([.. row.Options.Select(option => new JsonObject { ["value"] = option.Value, ["expression"] = option.Expression })]),
        })]),
        ["logicKeys"] = new JsonArray([.. logic.Select(key => new JsonObject
        {
            ["key"] = key.Name,
            ["readFrom"] = key.ReadFrom,
            ["text"] = key.IsMap,
            ["values"] = new JsonArray([.. key.Values.Select(value => new JsonObject { ["value"] = value.Value, ["expression"] = value.Expression.Trim() })]),
        })]),
    };

    private static IEnumerable<RowOption> Options(LogicKey key) => key.IsMap
        ? key.Values.Select(value => new RowOption(value.Value.ToLowerInvariant(), Words().Replace(value.Value, " $1"), value.Expression.Trim()))
        : [new RowOption("true", "Yes", key.Values[0].Expression.Trim()), new RowOption("false", "No", string.Empty)];

    private static bool Covers(DecisionRow row, LogicKey key)
    {
        var options = key.Values
            .Select(value => row.Options.FirstOrDefault(option => value.Nodes.All(Positive(option.Expression).Contains)))
            .ToList();

        return options.All(option => option != null) && options.Distinct().Count() == options.Count;
    }

    private static HashSet<string> Positive(string expression) =>
        [.. Node().Matches(expression).Where(match => !match.Groups["not"].Success).Select(match => match.Groups["id"].Value.ToUpperInvariant())];

    private static List<RowOption> Distinct(List<RowOption> options)
    {
        var result = new List<RowOption>();
        foreach (var option in options)
        {
            var value = option.Value;
            while (result.Any(existing => existing.Value == value))
                value += "_";

            result.Add(option with { Value = value });
        }

        return result;
    }

    private static string Label(string text)
    {
        var plain = TelltaleMarkup.Strip(text);
        if (PlayerText().Match(plain) is { Success: true } match)
            return char.ToUpperInvariant(match.Groups[1].Value[0]) + match.Groups[1].Value[1..];

        return PlayerShare().Replace(plain, "you");
    }

    private static string Slug(string label) =>
        string.Join('_', NonWord().Split(label.ToLowerInvariant()).Where(word => word.Length > 0).Take(SlugWords));

    private static int EpisodeOf(string logicKey, int season)
    {
        var digits = logicKey.StartsWith(EpisodePrefix, StringComparison.Ordinal)
            ? new string([.. logicKey.Skip(EpisodePrefix.Length).TakeWhile(char.IsDigit)])
            : string.Empty;

        return int.TryParse(digits, out var number) && number / EpisodeBase == season ? number % EpisodeBase : 0;
    }

    [GeneratedRegex(@"(?<not>~\s*)?\{?(?<id>[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\}?")]
    private static partial Regex Node();

    [GeneratedRegex(@"\{[0-9A-Fa-f-]{36}\}")]
    private static partial Regex Braced();

    [GeneratedRegex(@"^You and [\d.]+ ?%(?: of players)? (.+?)\.?$")]
    private static partial Regex PlayerText();

    [GeneratedRegex(@"you and [\d.]+ ?% of players", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerShare();

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex Words();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonWord();
}
