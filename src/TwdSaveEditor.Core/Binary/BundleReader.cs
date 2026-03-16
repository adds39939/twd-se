using System.IO.Compression;
using System.Text;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary;

/// <summary>
/// Reads a TWD Definitive Series .bundle save file.
/// Bundle = outer MetaStream with file table in default section + inner MetaStreams in async section.
/// Supports TTCZ-compressed sections.
/// </summary>
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

        // Parse outer MetaStream header
        var header = ReadMetaStreamHeader(reader);

        // Read and decompress sections
        var defaultData = ReadSection(reader, header.DefaultSectionSize);
        var debugData = ReadSection(reader, header.DebugSectionSize);
        var asyncData = ReadSection(reader, header.AsyncSectionSize);

        // Parse file table from default section
        var fileTable = ParseFileTable(defaultData);

        // Parse inner files from async section
        var psReader = new PropertySetReader();
        PropertySet? metadata = null;
        PropertySet? choices = null;
        string choicesFileName = "choices.prop";
        byte[]? rawMetadata = null;
        byte[]? rawChoices = null;
        var rawInnerFiles = new Dictionary<string, byte[]>();

        foreach (var entry in fileTable)
        {
            if (entry.Offset + entry.Size > asyncData.Length)
                continue; // Skip entries that point outside available data

            var innerData = new byte[entry.Size];
            Array.Copy(asyncData, entry.Offset, innerData, 0, entry.Size);
            rawInnerFiles[entry.Name] = innerData;

            try
            {
                var propData = ExtractDefaultSection(innerData);

                if (entry.Name == "metadata_slot.p")
                {
                    rawMetadata = innerData;
                    metadata = psReader.Read(propData);
                }
                else if (entry.Name is "choices.prop" or "season1.prop")
                {
                    // S1 uses choices.prop, S2 uses season1.prop for imported choices
                    rawChoices = innerData;
                    choicesFileName = entry.Name;
                    choices = psReader.Read(propData);
                }
            }
            catch
            {
                // Inner file parsing failed — preserve raw data for round-tripping
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
            ChoicesFileName = choicesFileName,
            RawBundleData = data,
            RawMetadataFile = rawMetadata,
            RawChoicesFile = rawChoices,
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

    /// <summary>
    /// Read a MetaStream section, decompressing TTCZ if the compressed flag is set.
    /// </summary>
    private static byte[] ReadSection(BinaryReaderEx reader, uint sizeField)
    {
        bool compressed = (sizeField & 0x80000000) != 0;
        var rawSize = (int)(sizeField & 0x7FFFFFFF);

        if (rawSize == 0)
            return [];

        var rawBytes = reader.ReadBytes(rawSize);

        if (!compressed)
            return rawBytes;

        // Check for TTCZ magic
        if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
            return DecompressTtcz(rawBytes);

        // Try zlib
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
            // Try raw deflate as last resort
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

        var unknown1 = reader.ReadUInt32(); // always 1
        var fileCount = reader.ReadUInt32();

        // Sanity check
        if (fileCount > 1000)
            return [];

        var entries = new List<BundleFileEntry>();
        for (uint i = 0; i < fileCount; i++)
        {
            if (reader.Remaining < 8)
                break;

            var offset = reader.ReadUInt32();
            var size = reader.ReadUInt32();

            // Read null-terminated filename, padded to 4-byte alignment
            var nameBytes = new List<byte>();
            byte b;
            while (reader.Remaining > 0 && (b = reader.ReadByte()) != 0)
                nameBytes.Add(b);
            var name = Encoding.ASCII.GetString(nameBytes.ToArray());

            // Pad to 4-byte alignment from start of name
            var nameLen = nameBytes.Count + 1; // including null
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

    /// <summary>
    /// Decompress a Telltale TTCZ (DataStreamContainer) compressed block.
    /// Format: magic(4) + window_size(4) + page_count(4) + (page_count+1)*offset_table(8 each) + compressed pages.
    /// Each page is raw DEFLATE compressed.
    /// </summary>
    private static byte[] DecompressTtcz(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        var magic = br.ReadUInt32();
        if (magic != 0x5454435A) // "TTCZ" as LE uint32
            throw new InvalidDataException($"Expected TTCZ magic 0x5454435A, got 0x{magic:X8}");

        var windowSize = br.ReadUInt32(); // max decompressed size per chunk (typically 65536)
        var pageCount = br.ReadUInt32();

        // Read page offset table: pageCount+1 entries of uint64
        var offsets = new ulong[pageCount + 1];
        for (int i = 0; i <= pageCount; i++)
            offsets[i] = br.ReadUInt64();

        // Decompress each page
        using var output = new MemoryStream();
        for (int i = 0; i < pageCount; i++)
        {
            var compStart = (long)offsets[i];
            var compLen = (int)(offsets[i + 1] - offsets[i]);
            ms.Position = compStart;
            var compressedBlock = br.ReadBytes(compLen);

            // Raw DEFLATE (no zlib/gzip header) — use DeflateStream
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

    /// <summary>
    /// Extract the default section data from an inner MetaStream file.
    /// </summary>
    private static byte[] ExtractDefaultSection(byte[] innerData)
    {
        using var ms = new MemoryStream(innerData);
        using var reader = new BinaryReaderEx(ms);

        var magic = reader.ReadUInt32();
        var defSize = reader.ReadUInt32();
        var dbgSize = reader.ReadUInt32();
        var asyncSize = reader.ReadUInt32();
        var verCount = reader.ReadUInt32();

        // Skip version entries
        for (uint i = 0; i < verCount; i++)
        {
            reader.ReadUInt64(); // type CRC
            reader.ReadUInt32(); // version CRC
        }

        // Read default section (may be compressed)
        bool compressed = (defSize & 0x80000000) != 0;
        var rawSize = (int)(defSize & 0x7FFFFFFF);
        var rawBytes = reader.ReadBytes(rawSize);

        if (!compressed)
            return rawBytes;

        // Check for TTCZ
        if (rawBytes.Length >= 4 && BitConverter.ToUInt32(rawBytes, 0) == 0x5454435A)
            return DecompressTtcz(rawBytes);

        // Try zlib
        using var compressedStream = new MemoryStream(rawBytes);
        using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        zlibStream.CopyTo(decompressed);
        return decompressed.ToArray();
    }
}
