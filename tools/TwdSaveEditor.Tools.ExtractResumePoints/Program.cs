using System.Text.Json;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractResumePoints.Chapters;
using TwdSaveEditor.Tools.ExtractResumePoints.Items;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;
using TwdSaveEditor.Tools.ExtractResumePoints.Props;
using TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

const int FirstEpisode = 1;
const int LastEpisode = 5;
const int ItemSeason = 2;

if (args.Length != 1 || !int.TryParse(args[0], out var season) || season is not (2 or 3))
{
    Console.WriteLine("Usage: ExtractResumePoints <season: 2 | 3>");
    Console.WriteLine("Reads each episode's developer chapter menu and dialogs and writes the season's chapter list;");
    Console.WriteLine("for Season 2 also the inventory items and where they are picked up.");
    return 1;
}

var data = Path.Combine(ToolPaths.ToolsDirectory, "data");
var output = Path.Combine(ToolPaths.RepositoryRoot, "src", $"TwdSaveEditor.Season.S{season}", "Data");
var projectScripts = Path.Combine(data, "lua", $"WDC_pc_ProjectSeason{season}_data");
var meta = MetaReader.CreateDefault();

var nodeLists = Path.Combine(output, $"s{season}.nodes.json");
var expressions = Path.Combine(output, $"s{season}.decisions.json");
var imported = season == ItemSeason
    ? new ImportedKeyReader(meta).Read(Path.Combine(data, "extracted", "WDC_pc_Project_data"))
    : [];
if (imported == null || !Directory.Exists(projectScripts) || !(File.Exists(nodeLists) || File.Exists(expressions)))
{
    Console.Error.WriteLine($"Extract WDC_pc_Project_data, decompile WDC_pc_ProjectSeason{season}_data and run ExtractDecisions {season} first.");
    return 1;
}

var loader = new DialogLoader(meta);
var decisions = File.Exists(nodeLists) ? DecisionNodeReader.ReadNodeLists(nodeLists, season) : DecisionNodeReader.ReadExpressions(expressions);
var reader = new EpisodeReader(data, season, loader, ConstantReader.Read(projectScripts), decisions);
var itemReader = season == ItemSeason ? new EpisodeItemReader(data, season, meta, loader) : null;
var episodes = new List<EpisodeResume>();
var inventories = new List<EpisodeItems>();
for (var number = FirstEpisode; number <= LastEpisode; number++)
{
    var episode = reader.Read(number);
    if (episode == null)
    {
        Console.Error.WriteLine($"Episode {number}: extract its .dlog files with ExtractArchive and decompile its scripts first.");
        return 1;
    }

    episodes.Add(episode);
    if (itemReader == null)
        continue;

    inventories.Add(itemReader.Read(episode));
    if (inventories[^1].Items.Count == 0)
    {
        Console.Error.WriteLine($"Episode {number}: extract ui_item_*.prop, {ItemCatalogReader.TextDialog} and {ItemCatalogReader.TextDatabase} with ExtractArchive first.");
        return 1;
    }
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

var options = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, $"s{season}.chapters.json"), document.ToJsonString(options) + Environment.NewLine);

var inventory = new JsonObject
{
    ["episodes"] = new JsonArray([.. inventories.Select(episode => new JsonObject
    {
        ["episode"] = episode.Episode,
        ["items"] = new JsonArray([.. episode.Items.Select(item => new JsonObject { ["id"] = item.Id, ["key"] = item.Key, ["name"] = item.Name })]),
        ["starting"] = new JsonArray([.. episode.Starting.Select(item => new JsonObject
        {
            ["item"] = item.Item,
            ["requires"] = Strings(item.Requires),
            ["unless"] = Strings(item.Unless),
        })]),
        ["chapters"] = new JsonArray([.. episode.Chapters.Select(chapter => new JsonObject
        {
            ["id"] = chapter.Id,
            ["carried"] = Strings(chapter.Carried),
            ["fromStart"] = Strings(chapter.FromStart),
        })]),
    })]),
};

if (itemReader != null)
    File.WriteAllText(Path.Combine(output, $"s{season}.items.json"), inventory.ToJsonString(options) + Environment.NewLine);

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

foreach (var episode in inventories)
{
    Console.WriteLine($"Episode {episode.Episode}: {episode.Items.Count} items ({string.Join(", ", episode.Items.Select(item => item.Name))}), starts with {string.Join(", ", episode.Starting.Select(item => item.Item))}");
    foreach (var chapter in episode.Chapters)
        Console.WriteLine($"  {chapter.Id,-28} {string.Join(" ", chapter.Carried)} | {string.Join(" ", chapter.FromStart)}");
}

Console.WriteLine($"Wrote {episodes.Sum(episode => episode.Points.Count)} resume points to {output}");
return 0;

static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(value => JsonValue.Create(value))]);
