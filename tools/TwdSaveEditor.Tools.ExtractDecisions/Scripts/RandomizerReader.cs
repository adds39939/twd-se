using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractDecisions.Model;

namespace TwdSaveEditor.Tools.ExtractDecisions.Scripts;

public static partial class RandomizerReader
{
    private const string GenerateFunction = "function ChoiceRandomizer_GenerateChoices";

    public static List<RandomDecision> Read(string script)
    {
        var split = script.IndexOf(GenerateFunction, StringComparison.Ordinal);
        var checks = script[..split];
        var generate = script[split..];

        var decisions = new List<RandomDecision>();
        foreach (Match block in EpisodeBlock().Matches(checks))
        {
            var episode = int.Parse(block.Groups[1].Value) - 1;
            foreach (Match check in Check().Matches(block.Groups[2].Value))
            {
                var id = check.Groups[2].Value;
                var body = Generated(id).Match(generate).Groups[1].Value;
                var random = RandomEvent().Match(body);
                decisions.Add(new RandomDecision(
                    id,
                    episode,
                    [.. Evaluate().Matches(check.Groups[1].Value).Select(expression => NodeIds.In(expression.Groups[1].Value))],
                    random.Success ? NodeIds.In(random.Groups[1].Value) : [],
                    [.. DialogEvent().Matches(body).SelectMany(single => NodeIds.In(single.Groups[1].Value))]));
            }
        }

        return decisions;
    }

    private static Regex Generated(string id) =>
        new($@"^    if notSeen{id}\b[^\n]*then\n(.*?)^    end$", RegexOptions.Multiline | RegexOptions.Singleline);

    [GeneratedRegex(@"episodeNum == (\d) then\n(.*?)(?=^  elseif episodeNum|^  end$)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex EpisodeBlock();

    [GeneratedRegex(@"if (not ChoiceStats_Evaluate[^\n]*) then\n\s+notSeen(\w+) = true")]
    private static partial Regex Check();

    [GeneratedRegex("ChoiceStats_Evaluate\\(\"([^\"]+)\"\\)")]
    private static partial Regex Evaluate();

    [GeneratedRegex(@"CreateRandomEvent\(\{(.*?)\}\)", RegexOptions.Singleline)]
    private static partial Regex RandomEvent();

    [GeneratedRegex("CreateDialogEvent\\(\"([^\"]+)\"\\)")]
    private static partial Regex DialogEvent();
}
