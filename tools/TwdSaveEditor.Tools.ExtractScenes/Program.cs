using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Json;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractScenes.Scenes;

var archivesDirectory = ToolPaths.Archives(args);
var cipher = new BlowfishV7(KeyFile.Load());

var episodes = new SortedDictionary<string, EpisodeContents>(StringComparer.Ordinal);

foreach (var (episodeId, archiveName) in EpisodeArchives.ByEpisode)
{
    var archivePath = Path.Combine(archivesDirectory, archiveName);
    if (!File.Exists(archivePath))
    {
        Console.WriteLine($"  SKIP {episodeId}: {archiveName} not found");
        continue;
    }

    Console.Write($"  Scanning {episodeId}...");
    try
    {
        if (EpisodeScanner.Scan(archivePath, cipher) is not { } contents)
        {
            Console.WriteLine(" no data");
            continue;
        }

        episodes[episodeId] = contents;
        Console.WriteLine($" {contents.Scenes.Count} scenes, {contents.Dialogs.Count} dlogs");
    }
    catch (Exception e)
    {
        Console.WriteLine($" ERROR: {e.Message}");
    }
}

var json = new JsonObject();
foreach (var (episodeId, contents) in episodes)
{
    json[episodeId] = new JsonObject
    {
        ["scenes"] = new JsonArray([.. contents.Scenes.Select(scene => JsonValue.Create(scene))]),
        ["dlogs"] = new JsonArray([.. contents.Dialogs.Select(dialog => JsonValue.Create(dialog))]),
    };
}

var outputPath = Path.Combine(ToolPaths.ToolsDirectory, "data", "episode_scenes.json");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, JsonText.Serialize(json, escapeNonAscii: true));

Console.WriteLine();
Console.WriteLine($"Wrote {outputPath}");
foreach (var (episodeId, contents) in episodes)
{
    Console.WriteLine($"  {episodeId}: {TextFormat.QuoteList(contents.Scenes)}");
}