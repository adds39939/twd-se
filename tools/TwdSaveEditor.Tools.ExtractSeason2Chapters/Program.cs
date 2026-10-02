using System.Text.Json;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Chapters;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Model;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Props;
using TwdSaveEditor.Tools.ExtractSeason2Chapters.Scripts;

const int FirstEpisode = 1;
const int LastEpisode = 5;

var data = Path.Combine(ToolPaths.ToolsDirectory, "data");
var output = Path.Combine(ToolPaths.RepositoryRoot, "src", "TwdSaveEditor.Season.S2", "Data");
var projectScripts = Path.Combine(data, "lua", "WDC_pc_ProjectSeason2_data");
var meta = MetaReader.CreateDefault();

var imported = new ImportedKeyReader(meta).Read(Path.Combine(data, "extracted", "WDC_pc_Project_data"));
if (imported == null || !Directory.Exists(projectScripts))
{
    Console.Error.WriteLine("Extract WDC_pc_Project_data and decompile WDC_pc_ProjectSeason2_data first.");
    return 1;
}

var reader = new EpisodeReader(data, new DialogLoader(meta), ConstantReader.Read(projectScripts), DecisionNodeReader.Read(Path.Combine(output, "s2.nodes.json")));
var episodes = new List<EpisodeResume>();
for (var number = FirstEpisode; number <= LastEpisode; number++)
{
    var episode = reader.Read(number);
    if (episode == null)
    {
        Console.Error.WriteLine($"Episode {number}: extract its .dlog files with ExtractArchive and decompile its scripts first.");
        return 1;
    }

    episodes.Add(episode);
}

var document = new JsonObject
{
    ["importedKeys"] = new JsonArray([.. imported.Select(entry => new JsonObject
    {
        ["episode"] = entry.Key,
        ["keys"] = new JsonArray([.. entry.Value.Select(key => JsonValue.Create(key))]),
    })]),
    ["episodes"] = new JsonArray([.. episodes.Select(episode => new JsonObject
    {
        ["episode"] = episode.Episode,
        ["chapters"] = new JsonArray([.. episode.Points.Select(point => new JsonObject
        {
            ["id"] = point.Id,
            ["title"] = point.Title,
            ["group"] = point.Group,
            ["script"] = point.Script,
            ["chapterId"] = point.ChapterId,
            ["startsEpisode"] = point.StartsEpisode,
            ["flags"] = new JsonArray([.. point.Flags.Select(flag => new JsonObject { ["key"] = flag.Key, ["value"] = flag.Value.DeepClone() })]),
            ["decided"] = new JsonArray([.. point.Decided.Select(key => JsonValue.Create(key))]),
        })]),
    })]),
};

File.WriteAllText(Path.Combine(output, "s2.chapters.json"), document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);

foreach (var episode in episodes)
{
    Console.WriteLine($"Episode {episode.Episode}: {episode.Points.Count} resume points, {episode.Chapters.Count} game chapters");
    foreach (var chapter in episode.Chapters)
        Console.WriteLine($"  {chapter.ChapterId,-16} {chapter.Dialog,-58} {string.Join(", ", chapter.Scripts)}");

    foreach (var point in episode.Points)
        Console.WriteLine($"  {point.ChapterId,-16} {point.Title,-28} {point.Script,-40} flags={point.Flags.Count} decided={point.Decided.Count}");

    foreach (var key in episode.Unplaced)
        Console.WriteLine($"  no dialog holds the nodes of {key}");
}

Console.WriteLine($"Wrote {episodes.Sum(episode => episode.Points.Count)} resume points to {output}");
return 0;
