using System.IO.Compression;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.MetaStream;

public static class MetaStreamWriter
{
    public static byte[] Write(MetaStreamHeader originalHeader, byte[] defaultSectionData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriterEx(ms, leaveOpen: true);

        writer.WriteUInt32(originalHeader.Magic);

        byte[] sectionBytes;
        uint sizeField;

        if (originalHeader.IsDefaultCompressed)
        {
            sectionBytes = Compress(defaultSectionData);
            sizeField = (uint)sectionBytes.Length | 0x80000000;
        }
        else
        {
            sectionBytes = defaultSectionData;
            sizeField = (uint)sectionBytes.Length;
        }

        writer.WriteUInt32(sizeField);
        writer.WriteUInt32(originalHeader.DebugSectionSize);
        writer.WriteUInt32(originalHeader.AsyncSectionSize);

        writer.WriteUInt32((uint)originalHeader.VersionEntries.Count);
        foreach (var entry in originalHeader.VersionEntries)
        {
            writer.WriteUInt64(entry.TypeCrc);
            writer.WriteUInt32(entry.VersionCrc);
        }

        writer.WriteBytes(sectionBytes);

        writer.Flush();
        return ms.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }
}
