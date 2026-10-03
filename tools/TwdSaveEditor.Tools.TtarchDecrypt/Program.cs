using System.Text;
using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.TtarchDecrypt.Search;

Console.OutputEncoding = Encoding.UTF8;

var archivesDirectory = ToolPaths.Archives(args);

var key = KeyFile.Load();
Console.WriteLine($"Key length: {key.Length} bytes");

var archivePath = Path.Combine(archivesDirectory, "WDC_pc_ProjectSeason3_data.ttarch2");
Console.WriteLine($"Archive: {archivePath}");

Console.WriteLine();
Console.WriteLine("Initializing Blowfish v7 cipher...");
var cipher = new BlowfishV7(key);
Console.WriteLine($"P[0] after init: 0x{cipher.P[0]:X8}");
Console.WriteLine($"P[1] after init: 0x{cipher.P[1]:X8}");
Console.WriteLine($"S[0][0] after init: 0x{cipher.S[0][0]:X8}");

Console.WriteLine();
Console.WriteLine("Parsing ECTT archive...");
var data = EcttArchive.Read(archivePath, cipher, Console.Out);

if (data is { Length: > 0 })
{
    new ArchiveDataSearch(data, Console.Out).Report();
}