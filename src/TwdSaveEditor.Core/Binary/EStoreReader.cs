using System.IO.Compression;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary;

/// <summary>
/// Reads Telltale estore/epage files containing EventLog data.
/// Used by S3/Michonne saves to store choices as "Executing Dialog Node" events.
/// </summary>
public static class EStoreReader
{
    /// <summary>
    /// Read all EventLog entries from an estore file and its associated epage files.
    /// </summary>
    public static List<EventLogEntry> ReadEventLog(string estorePath)
    {
        var entries = new List<EventLogEntry>();
        var dir = Path.GetDirectoryName(estorePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(estorePath);
        // Strip "_id" suffix to get the slot base name, then re-add for page pattern
        // estore: _wd3_saveslot1_id.estore → pages: _wd3_saveslot1_id_Page*.epage

        // Read estore file itself (may contain overflow events in default section)
        var estoreData = File.ReadAllBytes(estorePath);
        var estoreSections = ReadMetaStreamSections(estoreData);
        if (estoreSections.defaultData.Length > 0)
            entries.AddRange(ParseEventsFromSection(estoreSections.defaultData));

        // Find and read all epage files
        var pagePattern = $"{baseName}_Page*.epage";
        var pageFiles = Directory.GetFiles(dir, pagePattern)
            .OrderBy(f => ExtractPageNumber(f))
            .ToList();

        foreach (var pageFile in pageFiles)
        {
            var pageData = File.ReadAllBytes(pageFile);
            var pageSections = ReadMetaStreamSections(pageData);
            if (pageSections.defaultData.Length > 0)
                entries.AddRange(ParseEventsFromSection(pageSections.defaultData));
        }

        return entries;
    }

    /// <summary>
    /// Read EventLog entries from a single epage file.
    /// </summary>
    public static List<EventLogEntry> ReadEPage(string epagePath)
    {
        var data = File.ReadAllBytes(epagePath);
        var sections = ReadMetaStreamSections(data);
        return ParseEventsFromSection(sections.defaultData);
    }

    /// <summary>
    /// Parse 42-byte EventLog records from a section's data.
    /// Skips any header/filename prefix before the records start.
    /// </summary>
    internal static List<EventLogEntry> ParseEventsFromSection(byte[] data)
    {
        var entries = new List<EventLogEntry>();
        if (data.Length < EventLogEntry.RecordSize) return entries;

        // Find where 42-byte records start.
        // Records begin with the pattern: 0A000000 22000000 01000000 00000000
        // (version=10, payload=34, count=1, padding=0)
        int startOffset = FindRecordStart(data);
        if (startOffset < 0) return entries;

        for (int pos = startOffset; pos + EventLogEntry.RecordSize <= data.Length; pos += EventLogEntry.RecordSize)
        {
            // Validate record header
            var version = BitConverter.ToUInt32(data, pos);
            var payload = BitConverter.ToUInt32(data, pos + 4);
            if (version != 0x0A || payload != 0x22)
                break; // End of record block

            var raw = new byte[EventLogEntry.RecordSize];
            Array.Copy(data, pos, raw, 0, EventLogEntry.RecordSize);

            var entry = new EventLogEntry
            {
                EventTypeHash = BitConverter.ToUInt64(data, pos + 16),
                ValueType = BitConverter.ToUInt32(data, pos + 24),
                ExtraFlag = data[pos + 28],
                NodeHash = BitConverter.ToUInt64(data, pos + 29),
                SequenceIndex = (uint)(data[pos + 37] | (data[pos + 38] << 8) | (data[pos + 39] << 16)),
                Trailing = BitConverter.ToUInt16(data, pos + 40),
                RawData = raw,
            };

            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Find the start offset of the first 42-byte record in the data.
    /// </summary>
    private static int FindRecordStart(byte[] data)
    {
        // Look for the record header pattern: 0A000000 22000000 01000000 00000000
        byte[] pattern = [0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Parse MSV6 MetaStream header and extract sections (decompressing if needed).
    /// </summary>
    internal static (byte[] defaultData, byte[] debugData, byte[] asyncData) ReadMetaStreamSections(byte[] data)
    {
        if (data.Length < 20) return ([], [], []);

        var magic = BitConverter.ToUInt32(data, 0);
        if (magic is not (MetaStreamHeader.MagicMsv5 or MetaStreamHeader.MagicMsv6))
            return ([], [], []);

        var defSize = BitConverter.ToUInt32(data, 4);
        var dbgSize = BitConverter.ToUInt32(data, 8);
        var asyncSize = BitConverter.ToUInt32(data, 12);
        var verCount = BitConverter.ToUInt32(data, 16);
        var pos = 20 + (int)verCount * 12;

        var sections = new byte[3][];
        foreach (var (idx, sizeField) in new[] { (0, defSize), (1, dbgSize), (2, asyncSize) })
        {
            bool compressed = (sizeField & 0x80000000) != 0;
            var rawSize = (int)(sizeField & 0x7FFFFFFF);
            if (rawSize == 0 || pos + rawSize > data.Length)
            {
                sections[idx] = [];
                pos += rawSize;
                continue;
            }

            var raw = new byte[rawSize];
            Array.Copy(data, pos, raw, 0, rawSize);
            pos += rawSize;

            if (!compressed)
            {
                sections[idx] = raw;
                continue;
            }

            // Try TTCZ decompression
            if (raw.Length >= 4 && BitConverter.ToUInt32(raw, 0) == 0x5454435A)
            {
                sections[idx] = DecompressTtcz(raw);
                continue;
            }

            // Try zlib
            try
            {
                using var ms = new MemoryStream(raw);
                using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                sections[idx] = output.ToArray();
            }
            catch
            {
                try
                {
                    using var ms = new MemoryStream(raw);
                    using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    deflate.CopyTo(output);
                    sections[idx] = output.ToArray();
                }
                catch
                {
                    sections[idx] = raw;
                }
            }
        }

        return (sections[0], sections[1], sections[2]);
    }

    private static byte[] DecompressTtcz(byte[] data)
    {
        var windowSize = BitConverter.ToUInt32(data, 4);
        var pageCount = BitConverter.ToUInt32(data, 8);
        var offsets = new ulong[pageCount + 1];
        for (int i = 0; i <= pageCount; i++)
            offsets[i] = BitConverter.ToUInt64(data, 12 + i * 8);

        using var output = new MemoryStream();
        for (int i = 0; i < pageCount; i++)
        {
            var compStart = (int)offsets[i];
            var compLen = (int)(offsets[i + 1] - offsets[i]);
            var block = new byte[compLen];
            Array.Copy(data, compStart, block, 0, compLen);

            using var compStream = new MemoryStream(block);
            using var deflate = new DeflateStream(compStream, CompressionMode.Decompress);
            var pageBuffer = new byte[windowSize];
            int totalRead = 0, bytesRead;
            while ((bytesRead = deflate.Read(pageBuffer, totalRead, (int)windowSize - totalRead)) > 0)
                totalRead += bytesRead;
            output.Write(pageBuffer, 0, totalRead);
        }
        return output.ToArray();
    }

    private static int ExtractPageNumber(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var pageIdx = name.LastIndexOf("Page", StringComparison.Ordinal);
        if (pageIdx < 0) return 0;
        var numStr = name[(pageIdx + 4)..];
        return int.TryParse(numStr, out var num) ? num : 0;
    }
}
