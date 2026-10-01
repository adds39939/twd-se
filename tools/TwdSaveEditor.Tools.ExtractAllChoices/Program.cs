using System.Text;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.ExtractAllChoices.Choices;
using TwdSaveEditor.Tools.ExtractAllChoices.Model;

Console.OutputEncoding = Encoding.UTF8;

var archivesDirectory = ToolPaths.Archives(args);

(string Season, string Archive)[] archives =
[
    ("Season 1", "WDC_pc_ProjectSeason1_data.ttarch2"),
    ("Season 2", "WDC_pc_ProjectSeason2_data.ttarch2"),
    ("Season 3", "WDC_pc_ProjectSeason3_data.ttarch2"),
    ("Michonne", "WDC_pc_ProjectSeasonM_data.ttarch2"),
    ("Season 4", "WDC_pc_ProjectSeason4_data.ttarch2"),
];

var key = KeyFile.Load();

Console.WriteLine("Initializing Blowfish v7 cipher...");
var reader = new ChoiceArchiveReader(new BlowfishV7(key), Console.Out);
Console.WriteLine("Cipher ready.");
Console.WriteLine();

var seasons = new List<(string Season, List<Choice>? Choices)>();
foreach (var (season, archive) in archives)
    seasons.Add((season, reader.Read(season, Path.Combine(archivesDirectory, archive))));

Console.WriteLine();
Console.WriteLine();
Console.WriteLine(new string('=', 70));
Console.WriteLine("GENERATING SUMMARY");
Console.WriteLine(new string('=', 70));

var summary = ChoiceSummary.Build(seasons);
var outputPath = Path.Combine(ToolPaths.ToolsDirectory, "all_choices_summary.txt");
File.WriteAllText(outputPath, summary);

Console.WriteLine();
Console.WriteLine($"Summary saved to: {outputPath}");
Console.WriteLine();
Console.WriteLine("--- SUMMARY ---");
Console.WriteLine(summary);
