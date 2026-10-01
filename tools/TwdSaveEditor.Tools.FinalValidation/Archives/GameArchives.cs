using TwdSaveEditor.Tools.Common.Archives;
using TwdSaveEditor.Tools.Common.Cryptography;
using TwdSaveEditor.Tools.Common.Text;

namespace TwdSaveEditor.Tools.FinalValidation.Archives;

public sealed class GameArchives(string archivesDirectory, BlowfishV7 cipher)
{
    private const int LuaHeaderSize = 4;

    public OrderedDictionary<string, ReadOnlyMemory<byte>>? ExtractFiles(string archiveName)
    {
        var archivePath = Path.Combine(archivesDirectory, archiveName);
        if (!File.Exists(archivePath))
            return null;

        var data = EcttArchive.Read(archivePath, cipher);
        return data == null ? null : InnerArchive.Parse(data);
    }

    public string ReadLua(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        if (bytes.Length >= LuaHeaderSize && bytes.StartsWith("LE"u8))
            return TextFormat.DecodeAscii(cipher.DecryptData(bytes[LuaHeaderSize..]));

        return TextFormat.DecodeAscii(bytes);
    }

    public static (string? Name, ReadOnlyMemory<byte>? Data) Find(OrderedDictionary<string, ReadOnlyMemory<byte>> files, string pattern)
    {
        foreach (var (name, data) in files)
        {
            if (name.ToLowerInvariant().Contains(pattern.ToLowerInvariant()))
                return (name, data);
        }

        return (null, null);
    }

    public static IEnumerable<string> LuaFiles(OrderedDictionary<string, ReadOnlyMemory<byte>> files) =>
        files.Keys.Where(name => name.ToLowerInvariant().EndsWith(".lua", StringComparison.Ordinal)).Take(10);
}
