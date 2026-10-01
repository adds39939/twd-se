using TwdSaveEditor.Tools.Common.MetaStreams;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: TwdSaveEditor.Tools.AnalyzeSaves <file>...");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Print the decompressed MetaStream section sizes of bundle, estore, epage or prop files.");
    return 1;
}

foreach (var path in args)
{
    var name = Path.GetFileName(path);
    Console.WriteLine(MetaStreamParser.Parse(File.ReadAllBytes(path)) is { } sections
        ? $"{name}: default={sections.Default.Length}, debug={sections.Debug.Length}, async={sections.Async.Length} bytes"
        : $"{name}: not a MetaStream file");
}

return 0;
