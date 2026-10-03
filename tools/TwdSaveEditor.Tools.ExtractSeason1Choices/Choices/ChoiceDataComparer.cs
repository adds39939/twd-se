using System.Text.Json;
using TwdSaveEditor.Tools.ExtractSeason1Choices.Model;

namespace TwdSaveEditor.Tools.ExtractSeason1Choices.Choices;

public static class ChoiceDataComparer
{
    private const int EpisodeBase = 100;
    private const int ExtraEpisode = 106;
    private const string ExtraEpisodeSeason = "s1_400days";

    public static List<string> Compare(IReadOnlyList<PersistentChoice> game, IEnumerable<string> dataFiles)
    {
        var problems = new List<string>();
        var editor = new Dictionary<string, (int Episode, HashSet<string> Values)>(StringComparer.Ordinal);

        foreach (var file in dataFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var choice in document.RootElement.EnumerateArray())
            {
                var values = choice.GetProperty("options").EnumerateArray()
                    .Select(option => option.GetProperty("value").GetString()!)
                    .ToHashSet(StringComparer.Ordinal);
                editor[choice.GetProperty("choiceKey").GetString()!] = (PersistentEpisode(choice), values);
            }
        }

        foreach (var choice in game)
        {
            if (!editor.TryGetValue(choice.Key, out var entry))
            {
                problems.Add($"missing from the editor: {choice.Key} (episode {choice.Episode})");
                continue;
            }

            if (entry.Episode != choice.Episode)
            {
                problems.Add($"{choice.Key}: editor has episode {entry.Episode}, game has {choice.Episode}");
            }

            var expected = choice.Options.Select(option => option.Value).ToHashSet(StringComparer.Ordinal);
            if (!expected.SetEquals(entry.Values))
            {
                problems.Add($"{choice.Key}: editor values [{string.Join(", ", entry.Values.Order())}], game values [{string.Join(", ", expected.Order())}]");
            }
        }

        foreach (var key in editor.Keys.Except(game.Select(choice => choice.Key)))
        {
            problems.Add($"not a game key: {key}");
        }

        return problems;
    }

    public static int EpisodeNumber(int persistentEpisode) => persistentEpisode - EpisodeBase;

    private static int PersistentEpisode(JsonElement choice) =>
        choice.GetProperty("seasonKey").GetString() == ExtraEpisodeSeason
            ? ExtraEpisode
            : EpisodeBase + choice.GetProperty("episode").GetInt32();
}
