using TwdSaveEditor.Tools.Common.Cryptography;

namespace TwdSaveEditor.Tools.Common.Archives;

public static class GameArchive
{
    private const int UncompressedHeaderSize = 12;

    private static readonly byte[] UncompressedMagic = "NCTT"u8.ToArray();

    public static OrderedDictionary<string, ReadOnlyMemory<byte>> ReadFiles(string path, BlowfishV7 cipher)
    {
        var data = ReadContainer(path, cipher);
        return data == null ? [] : InnerArchive.Parse(data);
    }

    private static byte[]? ReadContainer(string path, BlowfishV7 cipher)
    {
        var magic = new byte[4];
        using (var file = File.OpenRead(path))
        {
            file.ReadExactly(magic);
        }

        if (magic.AsSpan().SequenceEqual(EcttArchive.Magic))
        {
            return EcttArchive.Read(path, cipher);
        }

        if (magic.AsSpan().SequenceEqual(UncompressedMagic))
        {
            return File.ReadAllBytes(path)[UncompressedHeaderSize..];
        }

        return null;
    }
}
