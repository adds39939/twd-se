using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Names;
using TwdSaveEditor.Tools.ExtractChapters.Chapters;
using TwdSaveEditor.Tools.ExtractChapters.Items;
using TwdSaveEditor.Tools.ExtractChapters.Model;

const int FirstEpisode = 101;
const int LastEpisode = 106;

var data = Path.Combine(ToolPaths.ToolsDirectory, "data");
var loader = new DialogLoader(MetaReader.CreateDefault());
var reader = new EpisodeReader(data, loader, SymbolNames.LoadDefault());
var itemReader = new EpisodeItemReader(data, loader);
var episodes = new List<EpisodeChapters>();

for (var number = FirstEpisode; number <= LastEpisode; number++)
{
    var episode = reader.Read(number);
    if (episode == null)
    {
        Console.Error.WriteLine($"Episode {number}: extract its .dlog and .scene files with ExtractArchive and decompile its scripts first.");
        return 1;
    }

    episodes.Add(episode);
}

var options = new JsonSerializerOptions { WriteIndented = true };
var raw = new JsonArray([.. episodes.Select(episode => new JsonObject
{
    ["persistentEpisode"] = episode.Episode,
    ["chapters"] = new JsonArray([.. episode.Chapters.Select(chapter => new JsonObject
    {
        ["handler"] = chapter.Handler,
        ["title"] = chapter.Title,
        ["group"] = chapter.Group,
        ["script"] = chapter.Script,
        ["conditional"] = chapter.Conditional,
        ["assignments"] = new JsonArray([.. chapter.Assignments.Select(assignment => new JsonObject
        {
            ["agent"] = assignment.Agent,
            ["key"] = assignment.Key,
            ["value"] = assignment.Value?.DeepClone(),
            ["unresolved"] = assignment.Unresolved,
        })]),
    })]),
    ["transitions"] = new JsonArray([.. episode.Transitions.Select(transition => new JsonObject
    {
        ["dialog"] = transition.Dialog,
        ["node"] = ChapterPlanner.NodePrefix + transition.Node.ToString(CultureInfo.InvariantCulture),
        ["target"] = transition.Target,
    })]),
    ["checkpoints"] = new JsonArray([.. episode.Checkpoints.Select(checkpoint => new JsonObject
    {
        ["dialog"] = checkpoint.Dialog,
        ["node"] = ChapterPlanner.NodePrefix + checkpoint.Node.ToString(CultureInfo.InvariantCulture),
        ["item"] = checkpoint.Item,
        ["chapterId"] = checkpoint.ChapterId,
    })]),
    ["decisions"] = new JsonArray([.. episode.Decisions.Select(decision => new JsonObject
    {
        ["dialog"] = decision.Dialog,
        ["key"] = decision.Key,
        ["value"] = decision.Value,
    })]),
    ["sceneDialogs"] = new JsonObject(episode.SceneDialogs.OrderBy(scene => scene.Key, StringComparer.Ordinal).Select(scene =>
        KeyValuePair.Create<string, JsonNode?>(scene.Key, new JsonArray([.. scene.Value.Select(dialog => JsonValue.Create(dialog))])))),
})]);

var rawPath = Path.Combine(data, "season1_chapters.json");
File.WriteAllText(rawPath, raw.ToJsonString(options));
Console.WriteLine($"Wrote {rawPath}");

var planned = new JsonArray();
var inventories = new List<EpisodeItems>();
foreach (var episode in episodes)
{
    var planner = new ChapterPlanner(episode);
    var plan = planner.Plan();
    planned.Add(plan);
    inventories.Add(itemReader.Read(episode, plan, [.. episodes.SelectMany(entry => entry.Decisions)]));

    var chapters = plan["chapters"]!.AsArray();
    Console.WriteLine($"{episode.Episode}: {chapters.Count} chapters from {episode.Chapters.Count} menu entries, {plan["decisionFlags"]!.AsArray().Count} decision flags");
    foreach (var skipped in planner.Skipped)
    {
        Console.WriteLine($"    skipped {skipped}");
    }
}

var editorPath = Path.Combine(ToolPaths.RepositoryRoot, "src", "TwdSaveEditor.Season.S1", "Data", "s1.chapters.json");
File.WriteAllText(editorPath, planned.ToJsonString(options) + Environment.NewLine);
Console.WriteLine($"Wrote {editorPath}");

var inventory = new JsonObject
{
    ["episodes"] = new JsonArray([.. inventories.Select(episode => new JsonObject
    {
        ["episode"] = episode.Episode,
        ["items"] = new JsonArray([.. episode.Items.Select(item => new JsonObject
        {
            ["id"] = item.Key,
            ["agent"] = item.Agent,
            ["name"] = item.Name,
            ["flag"] = item.Flag,
            ["maxCount"] = item.MaxCount,
            ["choiceKey"] = item.ChoiceKey,
        })]),
        ["chapters"] = new JsonArray([.. episode.Chapters.Select(chapter => new JsonObject
        {
            ["id"] = chapter.Id,
            ["carried"] = new JsonArray([.. chapter.Carried.Select(item => JsonValue.Create(item))]),
        })]),
    })]),
};

var itemsPath = Path.Combine(ToolPaths.RepositoryRoot, "src", "TwdSaveEditor.Season.S1", "Data", "s1.items.json");
File.WriteAllText(itemsPath, inventory.ToJsonString(options) + Environment.NewLine);
Console.WriteLine($"Wrote {itemsPath}");
foreach (var episode in inventories)
{
    Console.WriteLine($"{episode.Episode}: {string.Join(", ", episode.Items.Select(item => item.MaxCount > 1 ? $"{item.Name} x{item.MaxCount}" : item.Name))}");
    foreach (var chapter in episode.Chapters.Where(chapter => chapter.Carried.Count > 0))
    {
        Console.WriteLine($"    {chapter.Id,-34} {string.Join(", ", chapter.Carried)}");
    }
}
return 0;
