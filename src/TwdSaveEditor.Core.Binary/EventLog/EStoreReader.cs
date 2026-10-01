using System.IO.Compression;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.EventLog;

public static class EStoreReader
{
    public static List<EventLogEntry> ReadEventLog(string estorePath)
    {
        var entries = new List<EventLogEntry>();
        var dir = Path.GetDirectoryName(estorePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(estorePath);

        var estoreData = File.ReadAllBytes(estorePath);
        var estoreSections = ReadMetaStreamSections(estoreData);
        if (estoreSections.defaultData.Length > 0)
            entries.AddRange(ParseEventsFromSection(estoreSections.defaultData));

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

    public static List<EventLogEntry> ReadEPage(string epagePath)
    {
        var data = File.ReadAllBytes(epagePath);
        var sections = ReadMetaStreamSections(data);
        return ParseEventsFromSection(sections.defaultData);
    }

    public static List<EventLogEntry> ParseEventsFromSection(byte[] data)
    {
        var entries = new List<EventLogEntry>();
        if (data.Length < EventLogEntry.RecordSize) return entries;

        int startOffset = FindRecordStart(data);
        if (startOffset < 0) return entries;

        for (int pos = startOffset; pos + EventLogEntry.RecordSize <= data.Length; pos += EventLogEntry.RecordSize)
        {
            var version = BitConverter.ToUInt32(data, pos);
            var payload = BitConverter.ToUInt32(data, pos + 4);
            if (version != 0x0A || payload != 0x22)
                break;

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

    private static int FindRecordStart(byte[] data)
    {
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

    public static (byte[] defaultData, byte[] debugData, byte[] asyncData) ReadMetaStreamSections(byte[] data)
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

            if (raw.Length >= 4 && BitConverter.ToUInt32(raw, 0) == 0x5454435A)
            {
                sections[idx] = DecompressTtcz(raw);
                continue;
            }

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
