using TwdSaveEditor.Tools.Common.Binary;

namespace TwdSaveEditor.Tools.Common.Compression;

public static class Ttcz
{
    public const uint Magic = 0x5454435A;

    public static bool HasMagic(ReadOnlySpan<byte> data) => data.Length >= 4 && Bytes.U32(data, 0) == Magic;

    public static byte[] Decompress(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || !HasMagic(data))
            return data.ToArray();

        var pageCount = Bytes.U32(data, 8);
        var offsets = new ulong[pageCount + 1];
        for (var i = 0; i < offsets.Length; i++)
            offsets[i] = Bytes.U64(data, 12 + i * 8L);

        using var result = new MemoryStream();
        for (var i = 0; i < pageCount; i++)
        {
            var block = Bytes.Slice(data, (long)offsets[i], (long)offsets[i + 1]);
            result.Write(Zlib.TryInflateAny(block, Zlib.RawWindow, Zlib.ZlibWindow) ?? block);
        }

        return result.ToArray();
    }
}
