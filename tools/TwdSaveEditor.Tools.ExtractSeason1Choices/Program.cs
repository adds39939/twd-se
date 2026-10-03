using System.Text.Json;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractSeason1Choices.Choices;

var archivesDirectory = ToolPaths.Archives(args);
var reader = new PersistentChoiceReader(new BlowfishV7(KeyFile.Load()), MetaReader.CreateDefault());
var choices = reader.Read(Path.Combine(archivesDirectory, PersistentChoiceReader.Archive));

var json = new JsonArray();
foreach (var choice in choices)
{
    json.Add(new JsonObject
    {
        ["persistentEpisode"] = choice.Episode,
        ["episode"] = ChoiceDataComparer.EpisodeNumber(choice.Episode),
        ["choiceKey"] = choice.Key,
        ["description"] = choice.Description ?? choice.Key,
        ["options"] = new JsonArray([.. choice.Options.Select(option => new JsonObject
        {
            ["label"] = option.Label,
            ["value"] = option.Value,
        })]),
    });
}

var outputPath = Path.Combine(ToolPaths.ToolsDirectory, "data", "season1_choices.json");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {choices.Count} choices to {outputPath}");

var dataDirectory = Path.Combine(ToolPaths.RepositoryRoot, "src", "TwdSaveEditor.Season.S1", "Data");
var problems = ChoiceDataComparer.Compare(choices, Directory.EnumerateFiles(dataDirectory, "*.choices.json"));
foreach (var problem in problems)
{
    Console.WriteLine($"  MISMATCH {problem}");
}

Console.WriteLine(problems.Count == 0 ? "The editor's Season 1 choice data matches the game." : $"{problems.Count} mismatches.");
return problems.Count == 0 ? 0 : 1;
