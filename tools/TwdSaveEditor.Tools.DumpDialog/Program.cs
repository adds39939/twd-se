using TwdSaveEditor.Tools.Common.Dialogs;
using TwdSaveEditor.Tools.Common.Meta;
using TwdSaveEditor.Tools.Common.Names;

if (args.Length < 2)
{
    Console.WriteLine("Usage: DumpDialog <output directory | -> <file.dlog>...");
    return 1;
}

var names = SymbolNames.LoadDefault();
var loader = new DialogLoader(MetaReader.CreateDefault());
var failed = 0;

foreach (var path in args[1..])
{
    try
    {
        var dialog = loader.Load(path);
        if (args[0] == "-")
        {
            new DialogPrinter(names, Console.Out).Print(dialog);
            continue;
        }

        var directory = Path.Combine(args[0], Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path)))!);
        Directory.CreateDirectory(directory);
        using var writer = new StreamWriter(Path.Combine(directory, Path.GetFileName(path) + ".txt"));
        new DialogPrinter(names, writer).Print(dialog);
    }
    catch (MetaFormatException e)
    {
        failed++;
        Console.Error.WriteLine(e.Message);
    }
}

Console.WriteLine($"{args.Length - 1 - failed} of {args.Length - 1} dialogs written.");
return failed == 0 ? 0 : 1;
