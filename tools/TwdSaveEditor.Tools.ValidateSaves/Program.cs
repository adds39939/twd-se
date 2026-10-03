using System.Text;
using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.ValidateSaves.Data;
using TwdSaveEditor.Tools.ValidateSaves.Validators;

Console.OutputEncoding = Encoding.UTF8;

var archivesDirectory = ToolPaths.Archives(args);
BlowfishV7? cipher = null;
var allPass = true;

Console.WriteLine(new string('=', 80));
Console.WriteLine("TWD SAVE EDITOR - GAME COMPATIBILITY VALIDATION");
Console.WriteLine(new string('=', 80));

Section("VALIDATION 1: Bundle File Structure");
foreach (var season in Seasons.All)
{
    SeasonHeader(season.Key);
    Report(BundleStructureValidator.Validate(season));
}

Section("VALIDATION 2: Metadata Property Hashes");

Console.WriteLine();
Console.WriteLine("--- CRC64 Implementation Verification ---");
foreach (var name in (string[])["bool", "int32", "String"])
{
    Console.WriteLine($"  CRC64('{name}') = 0x{TelltaleCrc64.Compute(name):X16}");
}

Console.WriteLine();
Console.WriteLine("--- Event Type Hash Verification ---");
foreach (var (name, expected) in SaveFormat.EventTypes)
{
    var hash = TelltaleCrc64.Compute(name);
    Console.WriteLine($"  CRC64('{name}') = 0x{hash:X16} {(hash == expected ? "MATCH" : "MISMATCH")} (expected 0x{expected:X16})");
    if (hash != expected)
    {
        allPass = false;
    }
}

foreach (var season in Seasons.All)
{
    SeasonHeader(season.Key);
    Console.WriteLine($"  Decrypting {season.Archive}...");
    Report(MetadataHashValidator.Validate(season, DecryptArchive(season.Archive)));
}

Section("VALIDATION 3: Choice Format Compatibility");
foreach (var season in Seasons.All)
{
    SeasonHeader(season.Key);
    Report(ChoiceFormatValidator.Validate(season));
}

Section("VALIDATION 4: EventLog Record Format (S3/Michonne)");
foreach (var season in (string[])[Seasons.Season3, Seasons.Michonne])
{
    SeasonHeader(season);
    Report(EstoreFormatValidator.Validate(season));
}

Section("VALIDATION 5: S3 ChoiceNodeMapping Hash Verification");
Report(Season3ChoiceHashValidator.Validate());

Console.WriteLine();
Console.WriteLine(new string('=', 80));
Console.WriteLine(allPass
    ? "OVERALL: ALL VALIDATIONS PASSED"
    : "OVERALL: SOME VALIDATIONS HAD MISMATCHES - SEE DETAILS ABOVE");
Console.WriteLine(new string('=', 80));

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 80));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 80));
}

static void SeasonHeader(string season)
{
    Console.WriteLine();
    Console.WriteLine($"--- {season} ---");
}

void Report(List<string> results)
{
    foreach (var result in results)
    {
        Console.WriteLine(result);
        if (result.Contains("MISMATCH"))
        {
            allPass = false;
        }
    }
}

byte[]? DecryptArchive(string archive)
{
    var archivePath = Path.Combine(archivesDirectory, archive);
    if (!File.Exists(archivePath))
    {
        Console.WriteLine($"  Archive not found: {archivePath}");
        return null;
    }

    cipher ??= new BlowfishV7(KeyFile.Load());
    return EcttArchive.Read(archivePath, cipher);
}
