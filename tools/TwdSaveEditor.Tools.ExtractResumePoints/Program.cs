using System.Text.Json;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Seasons;
using TwdSaveEditor.Tools.ExtractResumePoints.Chapters;
using TwdSaveEditor.Tools.ExtractResumePoints.Items;
using TwdSaveEditor.Tools.ExtractResumePoints.Model;
using TwdSaveEditor.Tools.ExtractResumePoints.Props;
using TwdSaveEditor.Tools.ExtractResumePoints.Scripts;

const int FirstEpisode = 1;
const int ScriptItemSeason = 2;

if (args.Length != 1 || GameSeason.Find(args[0]) is not { } season)
{
    Console.WriteLine($"Usage: ExtractResumePoints <season: {GameSeason.Arguments}>");
    Console.WriteLine("Reads each episode's developer chapter menu and dialogs and writes the season's chapter list;");
    Console.WriteLine("and the inventory items with where they are picked up.");
    return 1;
}

var data = Path.Combine(ToolPaths.ToolsDirectory, "data");
var output = season.DataDirectory(ToolPaths.RepositoryRoot);
var projectScripts = Path.Combine(data, "lua", season.ProjectArchive);
var scriptItems = season.Number == ScriptItemSeason;
var meta = MetaReader.CreateDefault();

var nodeLists = Path.Combine(output, $"{season.SeasonKey}.nodes.json");
var expressions = Path.Combine(output, $"{season.SeasonKey}.decisions.json");
var imported = scriptItems
    ? new ImportedKeyReader(meta).Read(Path.Combine(data, "extracted", "WDC_pc_Project_data"))
    : [];
if (imported == null || !Directory.Exists(projectScripts) || !(File.Exists(nodeLists) || File.Exists(expressions)))
{
    Console.Error.WriteLine($"Extract WDC_pc_Project_data, decompile {season.ProjectArchive} and run ExtractDecisions {season.Argument} first.");
    return 1;
}

var loader = new DialogLoader(meta);
var decisions = File.Exists(nodeLists) ? DecisionNodeReader.ReadNodeLists(nodeLists, season.Number) : DecisionNodeReader.ReadExpressions(expressions);
var reader = new EpisodeReader(data, season, loader, meta, ConstantReader.Read(projectScripts), decisions);
Func<EpisodeResume, EpisodeItems> readItems = scriptItems
    ? new EpisodeItemReader(data, season, meta, loader).Read
    : File.Exists(Path.Combine(projectScripts, CollectibleReader.Script))
        ? new CollectibleReader(projectScripts).Read
        : new LogicItemReader(data, season, loader).Read;
var episodes = new List<EpisodeResume>();
var inventories = new List<EpisodeItems>();
for (var number = FirstEpisode; number <= season.Episodes; number++)
{
    var episode = reader.Read(number);
    if (episode == null)
    {
        Console.Error.WriteLine($"Episode {number}: extract its .dlog files with ExtractArchive and decompile its scripts first.");
        return 1;
    }

    episodes.Add(episode);
    inventories.Add(readItems(episode));
    if (inventories[^1].Items.Count == 0 && scriptItems)
    {
        Console.Error.WriteLine($"Episode {number}: no inventory items found; extract ui_item_*.prop, {ItemCatalogReader.TextDialog} and {ItemCatalogReader.TextDatabase} with ExtractArchive first.");
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
        ["chapters"] = new JsonArray([.. episode.Points.Select(Chapter)]),
    })]),
};

var options = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, $"{season.SeasonKey}.chapters.json"), document.ToJsonString(options) + Environment.NewLine);

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

File.WriteAllText(Path.Combine(output, $"{season.SeasonKey}.items.json"), inventory.ToJsonString(options) + Environment.NewLine);

foreach (var episode in episodes)
{
    Console.WriteLine($"Episode {episode.Episode}: {episode.Points.Count} resume points, {episode.Chapters.Count} game chapters");
    foreach (var chapter in episode.Chapters)
    {
        Console.WriteLine($"  {chapter.ChapterId,-16} {chapter.Dialog,-58} {string.Join(", ", chapter.Scripts)}");
    }

    foreach (var point in episode.Points)
    {
        Console.WriteLine($"  {point.ChapterId,-16} {point.Title,-28} {point.Script,-40} flags={point.Flags.Count} decided={point.Decided.Count}");
    }

    foreach (var key in episode.Unplaced)
    {
        Console.WriteLine($"  no dialog holds the nodes of {key}");
    }
}

foreach (var episode in inventories)
{
    Console.WriteLine($"Episode {episode.Episode}: {episode.Items.Count} items ({string.Join(", ", episode.Items.Select(item => item.Name))}), starts with {string.Join(", ", episode.Starting.Select(item => item.Item))}");
    foreach (var chapter in episode.Chapters)
    {
        Console.WriteLine($"  {chapter.Id,-28} {string.Join(" ", chapter.Carried)} | {string.Join(" ", chapter.FromStart)}");
    }
}

Console.WriteLine($"Wrote {episodes.Sum(episode => episode.Points.Count)} resume points to {output}");
return 0;

static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(value => JsonValue.Create(value))]);

static JsonObject Chapter(ResumePoint point)
{
    var chapter = new JsonObject
    {
        ["id"] = point.Id,
        ["title"] = point.Title,
        ["group"] = point.Group,
        ["script"] = point.Script,
        ["chapterId"] = point.ChapterId,
        ["startsEpisode"] = point.StartsEpisode,
        ["flags"] = new JsonArray([.. point.Flags.Select(flag => new JsonObject { ["key"] = flag.Key, ["value"] = flag.Value.DeepClone() })]),
        ["decided"] = new JsonArray([.. point.Decided.Select(key => JsonValue.Create(key))]),
    };

    if (point.Entry != null)
    {
        chapter["dialog"] = point.Entry.Dialog;
        chapter["dialogNode"] = point.Entry.Node;
    }

    return chapter;
}
