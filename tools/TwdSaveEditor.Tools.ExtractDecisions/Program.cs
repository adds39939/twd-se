using System.Text.Json;
using System.Text.Json.Nodes;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Seasons;
using TwdSaveEditor.Tools.ExtractDecisions.Choices;
using TwdSaveEditor.Tools.ExtractDecisions.Props;
using TwdSaveEditor.Tools.ExtractDecisions.Scripts;

if (args.Length != 1 || GameSeason.Find(args[0]) is not { } game)
{
    Console.WriteLine($"Usage: ExtractDecisions <season: {GameSeason.Arguments}>");
    Console.WriteLine("Builds the season's decision list and node ids from persistent.prop and choice.prop,");
    Console.WriteLine("and for Season 2 the choice randomizer script, into the season project's Data folder.");
    return 1;
}

var season = game.Number;
var data = Path.Combine(ToolPaths.ToolsDirectory, "data");
var randomizer = Path.Combine(data, "lua", game.MenuArchive, "ChoiceRandomizer.lua");
var project = Path.Combine(data, "extracted", game.ProjectArchive);
var persistent = Path.Combine(project, "persistent.prop");
var stats = Path.Combine(project, "choice.prop");

foreach (var path in new[] { persistent, stats })
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Missing {path}: extract it with ExtractArchive first.");
        return 1;
    }
}

var props = new GameProps(MetaReader.CreateDefault());
var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var output = game.DataDirectory(ToolPaths.RepositoryRoot);

if (!File.Exists(randomizer))
{
    var keys = props.ReadLogicKeys(persistent);
    if (!game.MatchesBracedNodes)
    {
        keys = [.. keys.Select(ExpressionDecisions.WithoutBracedNodes)];
    }

    var rows = ExpressionDecisions.Build(props.ReadStats(stats), keys, season);
    File.WriteAllText(Path.Combine(output, $"{game.SeasonKey}.choices.json"), new JsonArray([.. rows.Select(row => ExpressionDecisions.Choice(row, game.SeasonKey))]).ToJsonString(options) + Environment.NewLine);
    File.WriteAllText(Path.Combine(output, $"{game.SeasonKey}.decisions.json"), ExpressionDecisions.Decisions(rows, keys).ToJsonString(options) + Environment.NewLine);
    foreach (var row in rows)
    {
        Console.WriteLine($"{row.Episode} {(row.Story ? "story" : "stats")} {row.Key,-60} {string.Join(" | ", row.Options.Select(option => $"{option.Value}={option.Label}"))}");
    }

    Console.WriteLine($"Wrote {rows.Count} decisions and {keys.Count} logic keys to {output}");
    return 0;
}

var random = RandomizerReader.Read(File.ReadAllText(randomizer));
var decisions = DecisionMerger.Merge(random, props.ReadStats(stats), props.ReadLogicKeys(persistent), season);
var linked = random.Count > 0;

var indented = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, $"{game.SeasonKey}.choices.json"), new JsonArray([.. decisions.Select(decision => DecisionWriter.Choice(decision, season, linked))]).ToJsonString(indented) + Environment.NewLine);
File.WriteAllText(Path.Combine(output, $"{game.SeasonKey}.nodes.json"), new JsonArray([.. decisions.Select(decision => DecisionWriter.Nodes(decision, season))]).ToJsonString(indented) + Environment.NewLine);

foreach (var decision in decisions)
{
    var choice = DecisionWriter.Options(decision);
    var sources = string.Concat(decision.RandomizerId != null ? "R" : "-", decision.Stat != null ? "S" : "-", decision.Logic != null ? "L" : "-");
    Console.WriteLine($"{decision.Episode} {sources} {DecisionWriter.Key(decision, season),-48} " +
        string.Join(" | ", choice.Select((option, index) => $"{option.Value}={option.Label} ({decision.Options[index].Nodes.Count})")));
}

Console.WriteLine($"Wrote {decisions.Count} decisions to {output}");
return 0;
