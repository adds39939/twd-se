using System.Text;
using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Json;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ExtractNodeMappings.EventLog;
using TwdSaveEditor.Tools.ExtractNodeMappings.Mappings;
using TwdSaveEditor.Tools.ExtractNodeMappings.Model;
using TwdSaveEditor.Tools.ExtractNodeMappings.Reporting;

const string Season3Json = "s3_choice_prop.json";

Console.OutputEncoding = Encoding.UTF8;

var cipher = new BlowfishV7(KeyFile.Load());
var archivesDirectory = ToolPaths.Archives(args);
var savesRoot = ToolPaths.Argument(args, 1, ToolPaths.SampleSavesVariable);

Console.WriteLine(new string('=', 80));
Console.WriteLine("DIALOG NODE CRC64 HASH MAPPING EXTRACTOR");
Console.WriteLine(new string('=', 80));

Console.WriteLine();
Console.WriteLine(new string('=', 60));
Console.WriteLine($"SEASON 3 (from {Season3Json})");
Console.WriteLine(new string('=', 60));

var season3JsonPath = Path.Combine(ToolPaths.ToolsDirectory, Season3Json);
var season3Mappings = File.Exists(season3JsonPath)
    ? ChoiceMappingExtractor.FromJsonFile(season3JsonPath)
    : ExtractSeason3FromArchive();
Console.WriteLine($"  Extracted {season3Mappings.Count} choice entries from S3");

Console.WriteLine();
Console.WriteLine(new string('-', 40));
Console.WriteLine("Loading S3 epage files for verification...");
Console.WriteLine("(Using Episode 5 save which contains events from all episodes)");

var epageHashes = new EpageHashes();

string[] epageDirectories = savesRoot == null
    ? []
    : [Path.Combine(savesRoot, "S3", "Episode 1"), Path.Combine(savesRoot, "S3", "Episode 5", "The end")];

foreach (var directory in epageDirectories.Where(Directory.Exists))
{
    foreach (var epage in Directory.GetFiles(directory, "_wd3_saveslot1_id_Page*.epage").Order(StringComparer.Ordinal))
    {
        if (EpageHashReader.Read(epage, Console.Out) is not { } hashes)
        {
            continue;
        }

        Console.WriteLine($"  {Path.GetFileName(epage)}: {hashes.Nodes.Count} node hashes, {hashes.Choices.Count} choice hashes");
        epageHashes.Add(hashes);
    }
}

Console.WriteLine($"  Total unique 'Executing Dialog Node' hashes: {epageHashes.Nodes.Count}");
Console.WriteLine($"  Total unique 'Dialog Choice' hashes: {epageHashes.Choices.Count}");

Console.WriteLine();
Console.WriteLine("Initializing cipher...");
Console.WriteLine("Cipher ready.");

var seasonMappings = new Dictionary<string, List<ChoiceMapping>> { [Seasons.Season3] = season3Mappings };

foreach (var (season, archive) in Seasons.Archives)
{
    var archivePath = Path.Combine(archivesDirectory, archive);

    Console.WriteLine();
    Console.WriteLine(new string('=', 60));
    Console.WriteLine(season);
    Console.WriteLine(new string('=', 60));

    if (!File.Exists(archivePath))
    {
        Console.WriteLine($"  ERROR: Archive not found: {archivePath}");
        continue;
    }

    Console.WriteLine($"  Decrypting {archivePath}...");
    var data = EcttArchive.Read(archivePath, cipher);
    if (data is not { Length: > 0 })
    {
        Console.WriteLine("  ERROR: Failed to decrypt archive");
        continue;
    }

    Console.WriteLine($"  Decrypted size: {data.Length} bytes");

    var files = InnerArchive.Parse(data);
    Console.WriteLine($"  Files in archive: {files.Count}");

    var choiceData = ChoicePropLocator.Find(files, Console.Out);
    if (choiceData.IsEmpty)
    {
        Console.WriteLine($"  WARNING: No choice.prop found in {season}");
        Console.WriteLine($"  Available .prop files: {TextFormat.QuoteList(files.Keys.Where(ChoicePropLocator.IsPropFile).Take(20))}");
        continue;
    }

    var mappings = ChoiceMappingExtractor.FromBinaryProp(choiceData.Span, season, saveJson: true, Console.Out);
    seasonMappings[season] = mappings;
    Console.WriteLine($"  Extracted {mappings.Count} choice entries");
}

Console.WriteLine();
Console.WriteLine();
Console.WriteLine(new string('=', 80));
Console.WriteLine("ALL CHOICE -> DIALOG NODE HASH MAPPINGS");
Console.WriteLine(new string('=', 80));

var report = new MappingReport(seasonMappings, epageHashes).Build();
Console.WriteLine(report);

var textPath = Path.Combine(ToolPaths.ToolsDirectory, "node_hash_mappings.txt");
File.WriteAllText(textPath, report);
Console.WriteLine();
Console.WriteLine();
Console.WriteLine($"Mappings saved to: {textPath}");

var jsonPath = Path.Combine(ToolPaths.ToolsDirectory, "node_hash_mappings.json");
File.WriteAllText(jsonPath, JsonText.Serialize(MappingJson.Build(seasonMappings, epageHashes), escapeNonAscii: false));
Console.WriteLine($"JSON mappings saved to: {jsonPath}");

List<ChoiceMapping> ExtractSeason3FromArchive()
{
    var archivePath = Path.Combine(archivesDirectory, Seasons.Season3Archive);
    Console.WriteLine($"  {Season3Json} not found, reading choice.prop from {Seasons.Season3Archive}");
    if (!File.Exists(archivePath))
    {
        Console.WriteLine($"  ERROR: Archive not found: {archivePath}");
        return [];
    }

    var data = EcttArchive.Read(archivePath, cipher);
    var choiceData = data is { Length: > 0 } ? ChoicePropLocator.Find(InnerArchive.Parse(data), Console.Out) : default;
    if (choiceData.IsEmpty)
    {
        Console.WriteLine($"  WARNING: No choice.prop found in {Seasons.Season3}");
        return [];
    }

    return ChoiceMappingExtractor.FromBinaryProp(choiceData.Span, Seasons.Season3, saveJson: false, Console.Out);
}
