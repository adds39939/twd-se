using System.Text.Json;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;

namespace TwdSaveEditor.Tools.ExtractSeason2Chapters.Props;

public static class DecisionNodeReader
{
    private const string EpisodePrefix = "Episode 20";

    public static List<DecisionNodes> Read(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return
        [
            .. document.RootElement.EnumerateArray().Select(decision =>
            {
                var key = decision.GetProperty("choiceKey").GetString()!;
                var nodes = decision.GetProperty("options").EnumerateArray()
                    .SelectMany(option => option.GetProperty("nodes").EnumerateArray())
                    .Select(node => TelltaleCrc64.Compute($"{{{node.GetString()}}}"))
                    .ToList();
                return new DecisionNodes(key, key[EpisodePrefix.Length] - '0', nodes);
            }),
        ];
    }
}
