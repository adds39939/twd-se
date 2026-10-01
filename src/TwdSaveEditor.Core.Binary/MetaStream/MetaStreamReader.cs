using System.IO.Compression;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.MetaStream;

public static class MetaStreamReader
{
    public static (MetaStreamHeader Header, byte[] DefaultSection) Read(Stream stream)
    {
        using var reader = new BinaryReaderEx(stream, leaveOpen: true);

        var header = new MetaStreamHeader
        {
            Magic = reader.ReadUInt32()
        };

        if (header.Magic is not (MetaStreamHeader.MagicMsv5 or MetaStreamHeader.MagicMsv6))
            throw new InvalidDataException($"Unknown MetaStream magic: 0x{header.Magic:X8}");

        header.DefaultSectionSize = reader.ReadUInt32();
        header.DebugSectionSize = reader.ReadUInt32();
        header.AsyncSectionSize = reader.ReadUInt32();

        var versionCount = reader.ReadUInt32();
        for (uint i = 0; i < versionCount; i++)
        {
            var typeCrc = reader.ReadUInt64();
            var versionCrc = reader.ReadUInt32();
            header.VersionEntries.Add(new VersionEntry(typeCrc, versionCrc));
        }

        var defaultSection = ReadSection(reader, header.DefaultSectionSize);

        return (header, defaultSection);
    }

    public static (MetaStreamHeader Header, byte[] DefaultSection) Read(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return Read(fs);
    }

    private static byte[] ReadSection(BinaryReaderEx reader, uint sizeField)
    {
        bool compressed = (sizeField & 0x80000000) != 0;
        uint dataSize = sizeField & 0x7FFFFFFF;

        if (dataSize == 0)
            return [];

        var rawBytes = reader.ReadBytes((int)dataSize);

        if (!compressed)
            return rawBytes;

        using var compressedStream = new MemoryStream(rawBytes);
        using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        zlibStream.CopyTo(decompressed);
        return decompressed.ToArray();
    }
}
