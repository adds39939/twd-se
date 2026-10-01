using TwdSaveEditor.Tools.Common.Binary;
using TwdSaveEditor.Tools.Common.Compression;

namespace TwdSaveEditor.Tools.Common.Props;

public static class PropMetaStream
{
    public static byte[]? ReadDefinition(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20)
            return null;

        var sizeField = Bytes.U32(data, 4);
        var versionCount = Bytes.U32(data, 16);
        var compressed = (sizeField & 0x80000000) != 0;
        var size = sizeField & 0x7FFFFFFF;

        if (versionCount > 100)
            return null;

        var offset = 20 + versionCount * 12L;
        if (offset + size > data.Length)
            return null;

        var definition = data.Slice((int)offset, (int)size);
        if (!compressed || definition.Length < 4)
            return definition.ToArray();

        if (Ttcz.HasMagic(definition))
            return InflateBlocks(definition);

        return Zlib.TryInflateAny(definition, Zlib.ZlibWindow, Zlib.RawWindow) ?? definition.ToArray();
    }

    private static byte[] InflateBlocks(ReadOnlySpan<byte> data)
    {
        long offset = 16;
        var blockCount = Bytes.U32(data, offset);
        offset += 4;

        var blockSizes = new uint[blockCount];
        for (var i = 0; i < blockSizes.Length; i++)
        {
            blockSizes[i] = Bytes.U32(data, offset);
            offset += 4;
        }

        using var result = new MemoryStream();
        foreach (var blockSize in blockSizes)
        {
            var block = Bytes.Slice(data, offset, offset + blockSize);
            offset += blockSize;
            result.Write(Zlib.TryInflateAny(block, Zlib.ZlibWindow, Zlib.RawWindow) ?? block);
        }

        return result.ToArray();
    }
}
