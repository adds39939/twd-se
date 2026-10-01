using System.Text;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Text;
using TwdSaveEditor.Tools.ValidateEditedSaves.Bundles;

const string SlotBundle = "wd1_saveslot1.bundle";
const string AutosaveBundle = "_wd1_saveslot1_autosave.bundle";
const int ListedFiles = 5;

Console.OutputEncoding = Encoding.UTF8;

var saveDirectory = ToolPaths.Argument(args, 0, ToolPaths.SavesVariable);
var backupDirectory = ToolPaths.Argument(args, 1, ToolPaths.BackupVariable);

if (saveDirectory == null || backupDirectory == null)
{
    Console.Error.WriteLine("usage: TwdSaveEditor.Tools.ValidateEditedSaves [save_directory] [backup_directory]");
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Pass both directories or set {ToolPaths.SavesVariable} and {ToolPaths.BackupVariable}.");
    return 1;
}

Console.WriteLine(new string('=', 70));
Console.WriteLine("SAVE FILE VALIDATION");
Console.WriteLine(new string('=', 70));

(string Label, string Path)[] bundles =
[
    ("BACKUP SLOT", Path.Combine(backupDirectory, SlotBundle)),
    ("CURRENT SLOT", Path.Combine(saveDirectory, SlotBundle)),
    ("BACKUP AUTO", Path.Combine(backupDirectory, AutosaveBundle)),
    ("CURRENT AUTO", Path.Combine(saveDirectory, AutosaveBundle)),
];

foreach (var (label, path) in bundles)
{
    Console.WriteLine();
    if (!File.Exists(path))
    {
        Console.WriteLine($"{label}: FILE NOT FOUND");
        continue;
    }

    var bundle = BundleInspector.Inspect(path);
    Console.WriteLine($"{label} ({Path.GetFileName(path)}):");
    Console.WriteLine($"  Bundle size: {bundle.Size}");
    Console.WriteLine($"  Outer debug: {bundle.DebugSize} bytes = {TextFormat.Truncate(bundle.DebugHex, 80)}");
    Console.WriteLine($"  Async compressed: {bundle.AsyncCompressed}");
    Console.WriteLine($"  Files ({bundle.FileCount}):");

    foreach (var entry in bundle.Entries.Take(ListedFiles))
    {
        Console.WriteLine($"    {entry.Name}: offset={entry.Offset}, size={entry.Size}");
        if (!bundle.Files.TryGetValue(entry.Name, out var file))
            continue;

        Console.WriteLine($"      inner: def={file.DefaultSize}, dbg={file.DebugSize}, async={file.AsyncSize}, versions={file.Versions}");
        if (file.DebugHex != null)
            Console.WriteLine($"      dbg bytes: {file.DebugHex}");

        foreach (var (property, value) in file.Properties ?? [])
            Console.WriteLine($"      {property} = {(value is string text ? TextFormat.QuoteString(text) : value)}");

        if (file.PropertySet is { } header)
            Console.WriteLine($"      PS: ver={header.Version}, flags=0x{header.Flags:X}, data_size={header.DataSize}");
    }
}

Console.WriteLine();
Console.WriteLine(new string('=', 70));
Console.WriteLine("DIFFERENCES");
Console.WriteLine(new string('=', 70));

foreach (var fileName in (string[])[SlotBundle, AutosaveBundle])
{
    var backupPath = Path.Combine(backupDirectory, fileName);
    var currentPath = Path.Combine(saveDirectory, fileName);
    if (!File.Exists(backupPath) || !File.Exists(currentPath))
        continue;

    var backup = File.ReadAllBytes(backupPath);
    var current = File.ReadAllBytes(currentPath);
    Console.WriteLine();
    Console.WriteLine(backup.AsSpan().SequenceEqual(current)
        ? $"{fileName}: IDENTICAL (not edited yet)"
        : $"{fileName}: DIFFERENT (backup={backup.Length}, current={current.Length})");
}

return 0;
