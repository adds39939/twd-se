using System.Text.RegularExpressions;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;

namespace TwdSaveEditor.Tools.ExtractResumePoints.Items;

public sealed partial class CollectibleReader
{
    public const string Script = "Collectible.lua";

    private const string FoundPrefix = "Collectible Found - ";
    private const string PlacedPrefix = "Collectible Placed - ";
    private const string PlacedSuffix = " (placed)";

    private readonly List<List<string>> _episodes;

    public CollectibleReader(string projectScripts)
    {
        var text = File.ReadAllText(Path.Combine(projectScripts, Script));
        var table = Table().Match(text).Groups[1].Value;
        _episodes = [.. Group().Matches(table).Select(group => Name().Matches(group.Groups[1].Value).Select(match => match.Groups[1].Value).ToList())];
    }

    public EpisodeItems Read(EpisodeResume episode)
    {
        var names = episode.Episode <= _episodes.Count ? _episodes[episode.Episode - 1] : [];
        return new EpisodeItems(
            episode.Episode,
            [.. names.SelectMany(name => new[]
            {
                new Item(FoundPrefix + name, name, name),
                new Item(PlacedPrefix + name, name, name + PlacedSuffix),
            })],
            [],
            [.. episode.Points.Select(point => new ChapterItems(point.Id, [], []))]);
    }

    [GeneratedRegex("local kCollectibles = \\{(.*?)\\n\\}", RegexOptions.Singleline)]
    private static partial Regex Table();

    [GeneratedRegex("\\{([^{}]*)\\}")]
    private static partial Regex Group();

    [GeneratedRegex("\"([^\"]+)\"")]
    private static partial Regex Name();
}
