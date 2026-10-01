using System.IO.Compression;
using TwdSaveEditor.Core.Binary.Compression;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.MetaStream;

public static class MetaStreamCodec
{
    public static MetaStreamContent Read(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReaderEx(stream);

        var header = new MetaStreamHeader { Magic = reader.ReadUInt32() };
        if (header.Magic is not (MetaStreamHeader.MagicMsv5 or MetaStreamHeader.MagicMsv6))
            throw new InvalidDataException($"Unknown MetaStream magic: 0x{header.Magic:X8}");

        header.DefaultSectionSize = reader.ReadUInt32();
        header.DebugSectionSize = reader.ReadUInt32();
        header.AsyncSectionSize = reader.ReadUInt32();

        var versionCount = reader.ReadUInt32();
        for (uint i = 0; i < versionCount; i++)
            header.VersionEntries.Add(new VersionEntry(reader.ReadUInt64(), reader.ReadUInt32()));

        var defaultSection = ReadSection(reader, header.DefaultSectionSize);
        var debugSection = ReadSection(reader, header.DebugSectionSize);
        var asyncSection = ReadSection(reader, header.AsyncSectionSize);
        return new MetaStreamContent(header, defaultSection, debugSection, asyncSection);
    }

    public static byte[] Write(MetaStreamContent content)
    {
        var header = content.Header;
        var defaultSection = Encode(content.Default, header.IsDefaultCompressed, out var defaultSize);
        var debugSection = Encode(content.Debug, header.IsDebugCompressed, out var debugSize);
        var asyncSection = Encode(content.Async, header.IsAsyncCompressed, out var asyncSize);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriterEx(stream, leaveOpen: true);
        writer.WriteUInt32(header.Magic);
        writer.WriteUInt32(defaultSize);
        writer.WriteUInt32(debugSize);
        writer.WriteUInt32(asyncSize);
        writer.WriteUInt32((uint)header.VersionEntries.Count);
        foreach (var entry in header.VersionEntries)
        {
            writer.WriteUInt64(entry.TypeCrc);
            writer.WriteUInt32(entry.VersionCrc);
        }

        writer.WriteBytes(defaultSection);
        writer.WriteBytes(debugSection);
        writer.WriteBytes(asyncSection);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] Encode(byte[] section, bool compress, out uint sizeField)
    {
        if (!compress || section.Length == 0)
        {
            sizeField = (uint)section.Length;
            return section;
        }

        var compressed = Ttcz.Compress(section);
        sizeField = (uint)compressed.Length | MetaStreamHeader.CompressedFlag;
        return compressed;
    }

    private static byte[] ReadSection(BinaryReaderEx reader, uint sizeField)
    {
        var size = (int)(sizeField & ~MetaStreamHeader.CompressedFlag);
        if (size == 0)
            return [];

        var raw = reader.ReadBytes(size);
        if (raw.Length != size)
            throw new InvalidDataException("MetaStream section is truncated.");

        if ((sizeField & MetaStreamHeader.CompressedFlag) == 0)
            return raw;

        if (Ttcz.HasMagic(raw))
            return Ttcz.Decompress(raw);

        try
        {
            return Inflate(new ZLibStream(new MemoryStream(raw), CompressionMode.Decompress));
        }
        catch (InvalidDataException)
        {
            return Inflate(new DeflateStream(new MemoryStream(raw), CompressionMode.Decompress));
        }
    }

    private static byte[] Inflate(Stream inflater)
    {
        using (inflater)
        {
            using var output = new MemoryStream();
            inflater.CopyTo(output);
            return output.ToArray();
        }
    }
}
