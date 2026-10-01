using System.IO.Enumeration;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.ExtractArchive.Extraction;

if (args.Length < 2)
{
    Console.WriteLine("Usage: ExtractArchive <archive pattern> <output directory | --list> [file pattern...]");
    Console.WriteLine($"Archives are read from the directory in {ToolPaths.ArchivesVariable}.");
    return 1;
}

var archivesDirectory = Environment.GetEnvironmentVariable(ToolPaths.ArchivesVariable) ?? "Archives";
var extractor = new ArchiveExtractor(new BlowfishV7(KeyFile.Load()), Console.Out);

var archives = Directory.EnumerateFiles(archivesDirectory, "*.ttarch2")
    .Where(path => FileSystemName.MatchesSimpleExpression(args[0], Path.GetFileName(path), ignoreCase: true))
    .Order(StringComparer.OrdinalIgnoreCase)
    .ToList();

foreach (var archive in archives)
{
    if (args[1] == "--list")
    {
        Console.WriteLine($"== {Path.GetFileName(archive)}");
        extractor.List(archive);
    }
    else
    {
        extractor.Extract(archive, args[1], args[2..]);
    }
}

return 0;
