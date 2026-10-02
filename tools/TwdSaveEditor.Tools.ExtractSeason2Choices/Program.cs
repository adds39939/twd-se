using System.Text.Json;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.ExtractSeason2Choices.Choices;
using TwdSaveEditor.Tools.ExtractSeason2Choices.Props;
using TwdSaveEditor.Tools.ExtractSeason2Choices.Scripts;

var data = Path.Combine(ToolPaths.ToolsDirectory, "data");
var randomizer = Path.Combine(data, "lua", "WDC_pc_MenuSeason2_data", "ChoiceRandomizer.lua");
var project = Path.Combine(data, "extracted", "WDC_pc_ProjectSeason2_data");
var persistent = Path.Combine(project, "persistent.prop");
var stats = Path.Combine(project, "choice.prop");

foreach (var path in new[] { randomizer, persistent, stats })
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Missing {path}: extract it with ExtractArchive (and decompile the script) first.");
        return 1;
    }
}

var props = new GameProps(MetaReader.CreateDefault());
var decisions = DecisionMerger.Merge(RandomizerReader.Read(File.ReadAllText(randomizer)), props.ReadStats(stats), props.ReadLogicKeys(persistent));

var options = new JsonSerializerOptions { WriteIndented = true };
var output = Path.Combine(ToolPaths.RepositoryRoot, "src", "TwdSaveEditor.Season.S2", "Data");
File.WriteAllText(Path.Combine(output, "s2.choices.json"), new JsonArray([.. decisions.Select(DecisionWriter.Choice)]).ToJsonString(options) + Environment.NewLine);
File.WriteAllText(Path.Combine(output, "s2.nodes.json"), new JsonArray([.. decisions.Select(DecisionWriter.Nodes)]).ToJsonString(options) + Environment.NewLine);

foreach (var decision in decisions)
{
    var choice = DecisionWriter.Options(decision);
    var sources = string.Concat(decision.RandomizerId != null ? "R" : "-", decision.Stat != null ? "S" : "-", decision.Logic != null ? "L" : "-");
    Console.WriteLine($"{decision.Episode} {sources} {DecisionWriter.Key(decision),-48} " +
        string.Join(" | ", choice.Select((option, index) => $"{option.Value}={option.Label} ({decision.Options[index].Nodes.Count})")));
}

Console.WriteLine($"Wrote {decisions.Count} decisions to {output}");
return 0;
