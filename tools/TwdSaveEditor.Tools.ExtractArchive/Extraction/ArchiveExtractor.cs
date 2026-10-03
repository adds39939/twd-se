using System.IO.Enumeration;
using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Lua;

namespace TwdSaveEditor.Tools.ExtractArchive.Extraction;

public sealed class ArchiveExtractor(BlowfishV7 cipher, TextWriter output)
{
    private const string LuaExtension = ".lua";

    public int Extract(string archivePath, string outputDirectory, IReadOnlyList<string> patterns)
    {
        var files = GameArchive.ReadFiles(archivePath, cipher);
        var target = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(archivePath));
        var extracted = 0;

        foreach (var (name, content) in files)
        {
            if (!Matches(name, patterns))
            {
                continue;
            }

            Directory.CreateDirectory(target);
            File.WriteAllBytes(Path.Combine(target, name), Decode(name, content.Span));
            extracted++;
        }

        output.WriteLine($"{Path.GetFileName(archivePath)}: {extracted} of {files.Count} files");
        return extracted;
    }

    public void List(string archivePath)
    {
        foreach (var (name, content) in GameArchive.ReadFiles(archivePath, cipher))
        {
            output.WriteLine($"{content.Length,10}  {name}");
        }
    }

    private byte[] Decode(string name, ReadOnlySpan<byte> content) =>
        name.EndsWith(LuaExtension, StringComparison.OrdinalIgnoreCase)
            ? LuaScript.Decrypt(content, cipher)
            : content.ToArray();

    private static bool Matches(string name, IReadOnlyList<string> patterns) =>
        patterns.Count == 0 || patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true));
}
