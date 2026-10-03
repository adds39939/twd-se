using System.Globalization;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Constants;
using TwdSaveEditor.Season.S1.Chapters;
using TwdSaveEditor.Tools.BuildCheckpoint.Checkpoints;
using TwdSaveEditor.Core.Binary.SaveGames;

if (args.Length == 0)
{
    Console.WriteLine("Usage: BuildCheckpoint --verify <autosave bundle or directory>...");
    Console.WriteLine("       BuildCheckpoint strip <slot bundle> <autosave bundle> <slot number> <output directory> [--no-agents] [--keep <hex symbol>]...");
    Console.WriteLine("       BuildCheckpoint generate <slot bundle> <reference autosave> <slot number> <output directory> [--episode-flags]");
    Console.WriteLine("       BuildCheckpoint chapter <slot bundle> <episode> <slot number> <output directory> <chapter id> [choice=value]...");
    Console.WriteLine("       BuildCheckpoint chapters");
    return 1;
}

if (args[0] == "--verify")
{
    var paths = args[1..]
        .SelectMany(arg => Directory.Exists(arg) ? Directory.EnumerateFiles(arg, "_wd1_*.bundle", SearchOption.AllDirectories) : [arg])
        .ToList();

    var failures = 0;
    foreach (var path in paths)
    {
        var original = BundleReader.Read(path).FindFile(BundleFileNames.SaveGame)?.Data;
        if (original == null)
        {
            continue;
        }

        if (!SaveGameCodec.Write(SaveGameCodec.Read(original)).AsSpan().SequenceEqual(original))
        {
            failures++;
            Console.WriteLine($"FAIL {path}");
        }
    }

    Console.WriteLine($"{paths.Count - failures} of {paths.Count} default.save files are rewritten identically.");
    return failures == 0 ? 0 : 1;
}

if (args[0] == "chapters")
{
    foreach (var entry in S1ChapterCatalog.All)
    {
        foreach (var chapter in entry.Chapters)
        {
            Console.WriteLine($"{entry.Episode} {chapter.Id,-36} {chapter.Group,-8} {chapter.Title}");
        }
    }

    return 0;
}

if (args.Length < 5 || !int.TryParse(args[3], out var slotNumber))
{
    return 1;
}

var commands = new CheckpointCommands(args[4], Console.Out);
switch (args[0])
{
    case "strip":
        var keep = args.Zip(args.Skip(1))
            .Where(pair => pair.First == "--keep")
            .Select(pair => ulong.Parse(pair.Second, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToList();
        commands.Strip(args[1], args[2], slotNumber, !args.Contains("--no-agents"), keep);
        return 0;
    case "generate":
        commands.Generate(args[1], args[2], slotNumber, args.Contains("--episode-flags"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        return 0;
    case "chapter" when args.Length >= 6 && int.TryParse(args[2], out var episode):
        commands.Chapter(args[1], slotNumber, episode, args[5], args[6..], DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        return 0;
    default:
        return 1;
}
