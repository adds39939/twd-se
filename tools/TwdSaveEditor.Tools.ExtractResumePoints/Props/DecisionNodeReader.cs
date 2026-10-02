using System.Text.Json;
using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Props;

public static partial class DecisionNodeReader
{
    private const string EpisodePrefix = "Episode ";
    private const int EpisodesPerSeason = 100;

    public static List<DecisionNodes> ReadNodeLists(string path, int season)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return
        [
            .. document.RootElement.EnumerateArray().Select(decision =>
            {
                var key = decision.GetProperty("choiceKey").GetString()!;
                var nodes = decision.GetProperty("options").EnumerateArray()
                    .SelectMany(option => option.GetProperty("nodes").EnumerateArray())
                    .Select(node => Hash(node.GetString()!))
                    .ToList();
                return new DecisionNodes(key, EpisodeOf(key, season), nodes);
            }),
        ];
    }

    public static List<DecisionNodes> ReadExpressions(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return
        [
            .. document.RootElement.GetProperty("decisions").EnumerateArray().Select(decision => new DecisionNodes(
                decision.GetProperty("choiceKey").GetString()!,
                decision.GetProperty("episode").GetInt32(),
                [
                    .. decision.GetProperty("options").EnumerateArray()
                        .SelectMany(option => Node().Matches(option.GetProperty("expression").GetString()!))
                        .Select(match => Hash(match.Value)),
                ])),
        ];
    }

    private static ulong Hash(string node) => TelltaleCrc64.Compute($"{{{node}}}");

    private static int EpisodeOf(string key, int season)
    {
        var digits = key.StartsWith(EpisodePrefix, StringComparison.Ordinal)
            ? new string([.. key.Skip(EpisodePrefix.Length).TakeWhile(char.IsDigit)])
            : string.Empty;

        return int.TryParse(digits, out var number) && number / EpisodesPerSeason == season ? number % EpisodesPerSeason : 0;
    }

    [GeneratedRegex("[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}")]
    private static partial Regex Node();
}
