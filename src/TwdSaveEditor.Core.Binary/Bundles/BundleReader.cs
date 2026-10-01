using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Core.Binary.Primitives;
using TwdSaveEditor.Core.Binary.PropertySets;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Bundles;

public static class BundleReader
{
    public static SaveSlot Read(string filePath)
    {
        var rawData = File.ReadAllBytes(filePath);
        return Read(rawData, filePath);
    }

    public static SaveSlot Read(byte[] data, string filePath)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReaderEx(ms);

        var header = ReadMetaStreamHeader(reader);

        var defaultData = ReadSection(reader, header.DefaultSectionSize);
        var debugData = ReadSection(reader, header.DebugSectionSize);
        var asyncData = ReadSection(reader, header.AsyncSectionSize);

        var fileTable = ParseFileTable(defaultData);

        var psReader = new PropertySetReader();
        PropertySet? metadata = null;
        PropertySet? choices = null;
        PropertySet? choiceStats = null;
        string choicesFileName = "choices.prop";
        byte[]? rawMetadata = null;
        byte[]? rawChoices = null;
        byte[]? rawChoiceStats = null;
        var rawInnerFiles = new Dictionary<string, byte[]>();

        foreach (var entry in fileTable)
        {
            if (entry.Offset + entry.Size > asyncData.Length)
                continue;

            var innerData = new byte[entry.Size];
            Array.Copy(asyncData, entry.Offset, innerData, 0, entry.Size);
            rawInnerFiles[entry.Name] = innerData;

            if (entry.Name.StartsWith("_hash_"))
                continue;

            try
            {
                var propData = ExtractDefaultSection(innerData);

                if (entry.Name is "metadata_slot.p" or "metadata_save.p")
                {
                    rawMetadata = innerData;
                    metadata = psReader.Read(propData);
                }
                else if (entry.Name is "choices.prop" or "season1.prop")
                {
                    rawChoices = innerData;
                    choicesFileName = entry.Name;
                    choices = psReader.Read(propData);
                }
                else if (entry.Name == "choicestats.pro")
                {
                    rawChoiceStats = innerData;
                    choiceStats = psReader.Read(propData);
                }
            }
            catch
            {
            }
        }

        return new SaveSlot
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            OuterHeader = header,
            FileTable = fileTable,
            Metadata = metadata,
            Choices = choices,
            ChoiceStats = choiceStats,
            ChoicesFileName = choicesFileName,
            RawBundleData = data,
            RawMetadataFile = rawMetadata,
            RawChoicesFile = rawChoices,
            RawChoiceStatsFile = rawChoiceStats,
            RawInnerFiles = rawInnerFiles,
        };
    }

    private static MetaStreamHeader ReadMetaStreamHeader(BinaryReaderEx reader)
    {
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

        return header;
    }

    private static byte[] ReadSection(BinaryReaderEx reader, uint sizeField)
    {
        bool compressed = (sizeField & 0x80000000) != 0;
        var rawSize = (int)(sizeField & 0x7FFFFFFF);

        if (rawSize == 0)
            return [];

        var rawBytes = reader.ReadBytes(rawSize);

        if (!compressed)
            return rawBytes;

        if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
            return DecompressTtcz(rawBytes);

        try
        {
            using var compStream = new MemoryStream(rawBytes);
            using var zlib = new ZLibStream(compStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            using var compStream = new MemoryStream(rawBytes);
            using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }
    }

    private static List<BundleFileEntry> ParseFileTable(byte[] defaultData)
    {
        if (defaultData.Length < 8)
            return [];

        using var ms = new MemoryStream(defaultData);
        using var reader = new BinaryReaderEx(ms);

        var unknown1 = reader.ReadUInt32();
        var fileCount = reader.ReadUInt32();

        if (fileCount > 20000)
            return [];

        var entries = new List<BundleFileEntry>();
        for (uint i = 0; i < fileCount; i++)
        {
            if (reader.Remaining < 8)
                break;

            var offset = reader.ReadUInt32();
            var size = reader.ReadUInt32();

            var nameBytes = new List<byte>();
            byte b;
            while (reader.Remaining > 0 && (b = reader.ReadByte()) != 0)
                nameBytes.Add(b);

            var nameRaw = nameBytes.ToArray();
            var isReadable = nameRaw.Length > 0 && nameRaw.All(c => c >= 0x20 && c < 0x7F);
            var name = isReadable
                ? Encoding.ASCII.GetString(nameRaw)
                : $"_hash_{i:D4}";

            var nameLen = nameBytes.Count + 1;
            var padded = (nameLen + 3) & ~3;
            var skipBytes = padded - nameLen;
            if (skipBytes > 0 && reader.Remaining >= skipBytes)
                reader.ReadBytes(skipBytes);

            if (reader.Remaining < 16)
                break;

            var hash1 = reader.ReadUInt64();
            var hash2 = reader.ReadUInt64();

            entries.Add(new BundleFileEntry
            {
                Name = name,
                Offset = offset,
                Size = size,
                Hash1 = hash1,
                Hash2 = hash2,
            });
        }

        return entries;
    }

    private static byte[] DecompressTtcz(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        var magic = br.ReadUInt32();
        if (magic != 0x5454435A)
            throw new InvalidDataException($"Expected TTCZ magic 0x5454435A, got 0x{magic:X8}");

        var windowSize = br.ReadUInt32();
        var pageCount = br.ReadUInt32();

        var offsets = new ulong[pageCount + 1];
        for (int i = 0; i <= pageCount; i++)
            offsets[i] = br.ReadUInt64();

        using var output = new MemoryStream();
        for (int i = 0; i < pageCount; i++)
        {
            var compStart = (long)offsets[i];
            var compLen = (int)(offsets[i + 1] - offsets[i]);
            ms.Position = compStart;
            var compressedBlock = br.ReadBytes(compLen);

            using var compStream = new MemoryStream(compressedBlock);
            using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
            var pageBuffer = new byte[windowSize];
            int totalRead = 0;
            int bytesRead;
            while ((bytesRead = deflate.Read(pageBuffer, totalRead, (int)windowSize - totalRead)) > 0)
                totalRead += bytesRead;
            output.Write(pageBuffer, 0, totalRead);
        }

        return output.ToArray();
    }

    private static byte[] ExtractDefaultSection(byte[] innerData)
    {
        using var ms = new MemoryStream(innerData);
        using var reader = new BinaryReaderEx(ms);

        var magic = reader.ReadUInt32();
        var defSize = reader.ReadUInt32();
        var dbgSize = reader.ReadUInt32();
        var asyncSize = reader.ReadUInt32();
        var verCount = reader.ReadUInt32();

        for (uint i = 0; i < verCount; i++)
        {
            reader.ReadUInt64();
            reader.ReadUInt32();
        }

        bool compressed = (defSize & 0x80000000) != 0;
        var rawSize = (int)(defSize & 0x7FFFFFFF);
        var rawBytes = reader.ReadBytes(rawSize);

        if (!compressed)
            return rawBytes;

        if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
            return DecompressTtcz(rawBytes);

        using var compressedStream = new MemoryStream(rawBytes);
        using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        zlibStream.CopyTo(decompressed);
        return decompressed.ToArray();
    }
}
