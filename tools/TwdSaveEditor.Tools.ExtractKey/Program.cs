using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.ExtractKey.Keys;

string? gamePath = null;
var outputPath = ToolPaths.KeyFile;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-h" or "--help":
            Console.WriteLine("usage: TwdSaveEditor.Tools.ExtractKey [-h] [-o OUTPUT] [game_path]");
            Console.WriteLine();
            Console.WriteLine($"Extract the Blowfish archive key from {GameLocations.ExeName} and write it to tools/key.txt.");
            Console.WriteLine();
            Console.WriteLine($"  game_path     Game directory, its {GameLocations.ArchivesDirectoryName} directory, or {GameLocations.ExeName} itself (default: ${ToolPaths.ArchivesVariable})");
            Console.WriteLine("  -o, --output  Where to write the hex-encoded key");
            return 0;
        case "-o" or "--output" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        default:
            gamePath = args[i];
            break;
    }
}

gamePath ??= ToolPaths.Argument([], 0, ToolPaths.ArchivesVariable);
if (gamePath == null)
{
    return Fail($"Pass the game directory or set {ToolPaths.ArchivesVariable}.");
}

var exePath = GameLocations.FindExe(gamePath);
if (exePath == null)
{
    return Fail($"{GameLocations.ExeName} not found at or next to {GameLocations.Normalize(gamePath)}");
}

var exe = File.ReadAllBytes(exePath);
Console.WriteLine($"Executable: {exePath}");

var locator = new KeyLocator(exe, Console.Out);
var archivesDirectory = GameLocations.FindArchivesDirectory(exePath);
var probe = archivesDirectory == null ? null : ArchiveProbeReader.Read(archivesDirectory);

int offset;
if (probe != null)
{
    var found = locator.FindVerified(probe);
    if (found == null)
    {
        return Fail($"No key in {GameLocations.ExeName} decrypts {probe.ArchiveName}.");
    }

    offset = found.Value;
    Console.WriteLine($"Key found at 0x{offset:X}, verified against {probe.ArchiveName}");
}
else
{
    offset = KeyLocator.KnownOffset;
    if (!locator.HasKeyShape(offset))
    {
        return Fail(
            $"No {KeyLocator.KeyLength}-byte key at 0x{offset:X} and no encrypted archive to scan against. " +
            $"Set {ToolPaths.ArchivesVariable} so the key can be located and verified.");
    }

    Console.WriteLine($"Key read from 0x{offset:X}, unverified: no encrypted archive found to test it against");
}

File.WriteAllText(outputPath, Bytes.Hex(locator.KeyAt(offset)) + "\n");
Console.WriteLine($"Wrote {outputPath}");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
