using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Names;
using TwdSaveEditor.Tools.DumpBundle.Sections;

if (args.Length == 0)
{
    Console.WriteLine("Usage: DumpBundle <file.bundle | file.prop>...");
    Console.WriteLine("       DumpBundle --sections <output directory> <file>...");
    return 1;
}

if (args[0] == "--sections")
{
    foreach (var path in args[2..])
        SectionWriter.Write(path, args[1]);

    return 0;
}

var dumper = new MetaDumper(SymbolNames.LoadDefault(), MetaReader.CreateDefault(), Console.Out);

foreach (var path in args)
{
    Console.WriteLine($"=== {path}");
    var rootType = Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".bundle" => "ResourceBundle",
        ".estore" => "EventStorage",
        ".epage" => "EventStoragePage",
        ".save" => "SaveGame",
        ".dlog" => "Dlg",
        _ => "PropertySet",
    };
    dumper.DumpStream(File.ReadAllBytes(path), rootType);
    Console.WriteLine();
}

return 0;
